param branchName string
param cosmosDbUrl string
param cosmosDBDatabaseName string
param containerRegistry string

@secure()
param containerRegistryUsername string

@secure()
param cosmosDBConnectionString string

@secure()
param containerRegistryPassword string


@secure()
param cosmosDBKey string

param tags object = {}

param location string = resourceGroup().location

var BMSDAccountManagerImage = 'bmsd.managers.account:${branchName}'
var BMSDAccountManagerPort = 80
var BMSDAccountManagerIsExternalIngress = true

var BMSDNotificationManagerImage = 'bmsd.managers.notification:${branchName}'
var BMSDNotificationManagerPort = 80
var BMSDNotificationManagerIsExternalIngress = true

var BMSDUserInfoAccessorImage = 'bmsd.accessors.userinfo:${branchName}'
var BMSDUserInfoAccessorPort = 80
var BMSDUserInfoAccessorIsExternalIngress = false

var BMSDCheckingAccountAccessorImage = 'bmsd.accessors.checkingaccount:${branchName}'
var BMSDCheckingAccountAccessorPort = 80
var BMSDCheckingAccountAccessorIsExternalIngress = false

var BMSDLiabilityValidatorEngineImage  = 'bmsd.engines.liabilityvalidator:${branchName}'
var BMSDLiabilityValidatorEnginePort = 80
var BMSDLiabilityValidatorEngineIsExternalIngress = false


var minReplicas = 0
var maxReplicas = 1

var branch = toLower(last(split(branchName, '/')))

var signalRName = '${branch}-bmsd-signalr'

var environmentName = 'BankManagementSystemDemo'
var workspaceName = '${branch}-log-analytics'
var appInsightsName = '${branch}-app-insights'
var BMSDAccountManagerServiceContainerAppName = 'accountmanager' 
var BMSDNotificationManagerServiceContainerAppName = 'notificationmanager'
var BMSDUserInfoAccessorServiceContainerAppName = 'userinfoaccessor' 
var BMSDCheckingAccountAccessorServiceContainerAppName = 'checkingaccountaccessor'
var BMSDLiabilityValidatorEngineServiceContainerAppName = 'liabilityvalidatorengine' 


module signalr 'modules/signalr.bicep' = {
  name: 'signalrDeployment'
  params: {
    signalRName: signalRName
    location: location
  }
}
var signalrKey = signalr.outputs.signalrKey

module servicebus 'modules/servicebus.bicep' = {
  name: 'servicebusQueuesAndPubSubDeployment'
  params: {
    location: location
  }
}
var serviceBusConnectionString = servicebus.outputs.serviceBusConnectionString

module stateStore 'modules/dapr-component-statestore.bicep' = {
  name: 'cosmosDBStateStoreDeployment'
  params: {
     statestoreName: 'processedrequests'
     cosmosDbUrl : cosmosDbUrl
     masterKey : cosmosDBKey
	 databaseName : cosmosDBDatabaseName
	 collectionName : 'statestore'
	 environmentName: environmentName
	 appScope: [
	'${BMSDAccountManagerServiceContainerAppName}'
	]
  }
}

module containersAppInfra 'modules/containers-app-infra.bicep' = {
  name: 'containersAppInfraDeployment'
  params: {
    location: location
    appInsightsName: appInsightsName
    environmentName: environmentName
    workspaceName: workspaceName
    tags: tags
  }
}
var environmentId = containersAppInfra.outputs.environmentId


module daprComponentSignalr 'modules/dapr-component-signalr.bicep' = {
  name: 'daprComponentSignalRDeployment'
  params: {
    environmentName: environmentName
    signalrKey: signalrKey
    appScope: [
      BMSDNotificationManagerServiceContainerAppName
	  BMSDUserInfoAccessorServiceContainerAppName
	  BMSDCheckingAccountAccessorServiceContainerAppName
    ]
  }
  dependsOn:  [
    containersAppInfra
    signalr
  ]
}

module daprComponentAccountTransactionQueue 'modules/dapr-component-queue.bicep' = {
  name: 'daprComponentAccountTransactionQueueDeployment'
  params: {
    queueName:'accounttransactionqueue'
    environmentName: environmentName
    serviceBusConnectionString: serviceBusConnectionString
    appScope: [
	  '${BMSDAccountManagerServiceContainerAppName}'
      '${BMSDCheckingAccountAccessorServiceContainerAppName}'
    ]
  }
  dependsOn:  [
    containersAppInfra
    servicebus
  ]
}


module daprComponentCustomerRegistrationQueue 'modules/dapr-component-queue.bicep' = {
  name: 'daprComponentCustomerRegistrationQueueDeployment'
  params: {
    queueName:'customerregistrationqueue'
    environmentName: environmentName
    serviceBusConnectionString: serviceBusConnectionString
    appScope: [
	  '${BMSDAccountManagerServiceContainerAppName}'
      '${BMSDUserInfoAccessorServiceContainerAppName}'
    ]
  }
  dependsOn:  [
    containersAppInfra
    servicebus
  ]
}


module daprComponentClientResponseQueue 'modules/dapr-component-queue.bicep' = {
  name: 'daprComponentClientResponseQueueDeployment'
  params: {
    queueName:'clientresponsequeue'
    environmentName: environmentName
    serviceBusConnectionString: serviceBusConnectionString
    appScope: [
	  '${BMSDNotificationManagerServiceContainerAppName}'
      '${BMSDCheckingAccountAccessorServiceContainerAppName}'
	  '${BMSDUserInfoAccessorServiceContainerAppName}'
    ]
  }
  dependsOn:  [
    containersAppInfra
    servicebus
  ]
}



resource BMSDCheckingAccountAccessorContainerApp 'Microsoft.App/containerApps@2022-03-01' = {
  name: BMSDCheckingAccountAccessorServiceContainerAppName
  tags: tags
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      dapr: {
        enabled: true
        appPort: BMSDCheckingAccountAccessorPort
        appId: BMSDCheckingAccountAccessorServiceContainerAppName
        appProtocol: 'http'
      }
      secrets: [
        {
          name: 'container-registry-password-ref'
          value: containerRegistryPassword
        }
        {
          name: 'servicebuskeyref'
          value: serviceBusConnectionString
        }
      ]
      registries: [
        {
          server: containerRegistry
          username: containerRegistryUsername
          passwordSecretRef: 'container-registry-password-ref'
        }
      ]
      ingress: {
        external: BMSDCheckingAccountAccessorIsExternalIngress
        targetPort: BMSDCheckingAccountAccessorPort
      }
    }
    template: {
      containers: [
        {
          image: '${containerRegistry}/${BMSDCheckingAccountAccessorImage}'
          name: BMSDCheckingAccountAccessorServiceContainerAppName
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://localhost:80'
            }
            {
              name: 'CosmosDbConnectionString'
              value: cosmosDBConnectionString
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'queue-based-scaling'
            custom: {
              type: 'azure-servicebus'
              metadata: {
                queueName: 'accounttransactionqueue'
                messageCount: '1'
              }
              auth: [
                 {
                    secretRef: 'servicebuskeyref'
                    triggerParameter: 'connection'
                 }
                ]
            }
          }
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
          ]
       }
    }
  }
  dependsOn:  [
    containersAppInfra
    servicebus
  ]
}



resource BMSDUserInfoAccessorContainerApp 'Microsoft.App/containerApps@2022-03-01' = {
  name: BMSDUserInfoAccessorServiceContainerAppName
  tags: tags
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      dapr: {
        enabled: true
        appPort: BMSDUserInfoAccessorPort
        appId: BMSDUserInfoAccessorServiceContainerAppName
      }
      secrets: [
        {
          name: 'container-registry-password-ref'
          value: containerRegistryPassword
        }
        {
          name: 'servicebuskeyref'
          value: serviceBusConnectionString
        }
      ]
      registries: [
        {
          server: containerRegistry
          username: containerRegistryUsername
          passwordSecretRef: 'container-registry-password-ref'
        }
      ]
      ingress: {
        external: BMSDUserInfoAccessorIsExternalIngress
        targetPort: BMSDUserInfoAccessorPort
      }
    }
    template: {
      containers: [
        {
          image: '${containerRegistry}/${BMSDUserInfoAccessorImage}'
          name: BMSDUserInfoAccessorServiceContainerAppName
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://localhost:80'
            }
            {
              name: 'CosmosDbConnectionString'
			  value: cosmosDBConnectionString
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'queue-based-scaling'
            custom: {
              type: 'azure-servicebus'
              metadata: {
                queueName: 'customerregistrationqueue'
                messageCount: '1'
              }
              auth: [
                 {
                    secretRef: 'servicebuskeyref'
                    triggerParameter: 'connection'
                 }
                ]
            }
          }
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
          ]
       }
    }
  }
  dependsOn:  [
    containersAppInfra
    servicebus
  ]
}

resource BMSDLiabilityValidatorEngineContainerApp 'Microsoft.App/containerApps@2022-03-01' = {
  name: BMSDLiabilityValidatorEngineServiceContainerAppName
  tags: tags
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      dapr: {
        enabled: true
        appPort: BMSDLiabilityValidatorEnginePort
        appId: BMSDLiabilityValidatorEngineServiceContainerAppName
        appProtocol: 'http'
      }
      secrets: [
        {
          name: 'container-registry-password-ref'
          value: containerRegistryPassword
        }
        {
          name: 'servicebuskeyref'
          value: serviceBusConnectionString
        }
      ]
      registries: [
        {
          server: containerRegistry
          username: containerRegistryUsername
          passwordSecretRef: 'container-registry-password-ref'
        }
      ]
      ingress: {
        external: BMSDLiabilityValidatorEngineIsExternalIngress
        targetPort: BMSDLiabilityValidatorEnginePort
      }
    }
    template: {
      containers: [
        {
          image: '${containerRegistry}/${BMSDLiabilityValidatorEngineImage}'
          name: BMSDLiabilityValidatorEngineServiceContainerAppName
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://localhost:80'
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
          ]
       }
    }
  }
  dependsOn:  [
    containersAppInfra
    servicebus
  ]
}


resource BMSDNotificationManagerContainerApp 'Microsoft.App/containerApps@2022-03-01' = {
  name: BMSDNotificationManagerServiceContainerAppName
  tags: tags
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      dapr: {
        enabled: true
        appPort: BMSDNotificationManagerPort
        appId: BMSDNotificationManagerServiceContainerAppName
        appProtocol: 'http'
      }
      secrets: [
        {
          name: 'container-registry-password-ref'
          value: containerRegistryPassword
        }
      ]
      registries: [
        {
          server: containerRegistry
          username: containerRegistryUsername
          passwordSecretRef: 'container-registry-password-ref'
        }
      ]
      ingress: {
        external: BMSDNotificationManagerIsExternalIngress
        targetPort: BMSDNotificationManagerPort
      }
    }
    template: {
      containers: [
        {
          image: '${containerRegistry}/${BMSDNotificationManagerImage}'
          name: BMSDNotificationManagerServiceContainerAppName
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://localhost:80'
            }
			{
              name: 'AZURE__SignalR__ConnectionString'
              value: signalrKey
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    containersAppInfra
    servicebus
    signalr
  ]
}


resource BMSDAccountManagerContainerApp 'Microsoft.App/containerApps@2022-06-01-preview' = {
  name: BMSDAccountManagerServiceContainerAppName
  tags: tags
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      dapr: {
        enabled: true
        appPort: BMSDAccountManagerPort
        appId: BMSDAccountManagerServiceContainerAppName
        appProtocol: 'http'
        enableApiLogging: true
        logLevel:'debug'
      }
      secrets: [
        {
          name: 'container-registry-password-ref'
          value: containerRegistryPassword
        }
      ]
      registries: [
        {
          server: containerRegistry
          username: containerRegistryUsername
          passwordSecretRef: 'container-registry-password-ref'
        }
      ]
      ingress: {
        external: BMSDAccountManagerIsExternalIngress
        targetPort: BMSDAccountManagerPort
      }
    }
    template: {
      containers: [
        {
          image: '${containerRegistry}/${BMSDAccountManagerImage}'
          name: BMSDAccountManagerServiceContainerAppName
          resources: {
            cpu: json('0.5')
            memory: '1Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://localhost:80'
            }
          ]
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
        rules: [
          {
            name: 'http-rule'
            http: {
              metadata: {
                concurrentRequests: '20'
              }
            }
          }
        ]
      }
    }
  }
  dependsOn: [
    BMSDUserInfoAccessorContainerApp
    BMSDCheckingAccountAccessorContainerApp
    BMSDLiabilityValidatorEngineContainerApp
    containersAppInfra
    servicebus
  ]
}

output webServiceUrl string = BMSDAccountManagerContainerApp.properties.latestRevisionFqdn
