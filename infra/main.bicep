param branchName string
param cosmosDbUrl string
param cosmosDBDatabaseName string
param location string = resourceGroup().location

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


var uamiName = '${uniqueString(resourceGroup().id)}-uami'

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

var containerRegistryPasswordSecretName = 'acrpasswordsecretkey'

var signalRName = '${branch}-bmsd-signalr'
var signalRConnectionStringSecretKeyName = 'signalrsecretkey'

var servicebusNamespaceName = '${branch}-bmsd-servicebus'
var servicebusConnectionStringSecretKeyName = 'servicebussecretkey'


var keyVaultName = '${branch}-bmsd-keyvault'
var environmentName = '${branch}-bmsd-env'
var workspaceName = '${branch}-log-analytics'
var appInsightsName = '${branch}-app-insights'
var BMSDAccountManagerServiceContainerAppName = 'accountmanager' 
var BMSDNotificationManagerServiceContainerAppName = 'notificationmanager'
var BMSDUserInfoAccessorServiceContainerAppName = 'userinfoaccessor' 
var BMSDCheckingAccountAccessorServiceContainerAppName = 'checkingaccountaccessor'
var BMSDLiabilityValidatorEngineServiceContainerAppName = 'liabilityvalidatorengine' 

//create the assigned user managed identity
module uami 'modules/identity.bicep' = {
  name: uamiName
  params: {
    uamiName: uamiName
    location: location
  }
}
var managedIdentityObjectId = uami.outputs.principalId
var managedIdentityClientId = uami.outputs.clientId
var uamiId = uami.outputs.uamiId

//create the containers app required services
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


//create the secret keyvault
module keyvault 'modules/keyvault.bicep' = {
   name: keyVaultName
    params: {
        keyVaultName: keyVaultName
        location: location
        objectId: managedIdentityObjectId
        tenantId: tenant().tenantId
    }
    dependsOn: [
     uami   
    ]
}


//add the azure container registry password to the keyvault
resource containerRegistryPasswordSecret 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  name: '${keyVaultName}/${containerRegistryPasswordSecretName}'
  properties: {
    value: containerRegistryPassword
  }
  dependsOn: [
     keyvault   
    ]
  }

module signalr 'modules/signalr.bicep' = {
  name: 'signalrDeployment'
  params: {
    signalRName: signalRName
    keyvaultName: keyVaultName
    signalRConnectionStringSecretName: signalRConnectionStringSecretKeyName
    uamiId : uamiId
    location: location
  }
   dependsOn:  [
    keyvault
  ]
}
var signalRConnectionString = signalr.outputs.signalRConnectionString


module servicebus 'modules/servicebus.bicep' = {
  name: 'servicebusQueuesAndPubSubDeployment'
  params: {
    location: location
    servicebusNamespaceName: servicebusNamespaceName
    uamiId: uamiId
    keyvaultName: keyVaultName
    serviceBusConnectionStringSecretName: servicebusConnectionStringSecretKeyName
  }
  dependsOn:  [
    keyvault
  ]
}
var serviceBusConnectionString = servicebus.outputs.serviceBusConnectionString


module daprComponentSecretStore 'modules/dapr-component-secretstore.bicep' = {
  name: 'daprComponentSecretStoreDeployment'
  params: {
    keyvaultName: keyVaultName
    clientId: managedIdentityClientId
    environmentName: environmentName
    appScope: [
      BMSDAccountManagerServiceContainerAppName
      BMSDNotificationManagerServiceContainerAppName
      BMSDUserInfoAccessorServiceContainerAppName
      BMSDCheckingAccountAccessorServiceContainerAppName
      BMSDLiabilityValidatorEngineServiceContainerAppName
    ]
  }
  dependsOn:  [
    containersAppInfra
    keyvault
  ]
}


module daprComponentSignalr 'modules/dapr-component-signalr.bicep' = {
  name: 'daprComponentSignalRDeployment'
  params: {
    environmentName: environmentName
    signalRConnectionStringSecretName: signalRConnectionStringSecretKeyName
    signalRName: 'clientcallback'
    secretStoreName: keyVaultName
    appScope: [
      BMSDNotificationManagerServiceContainerAppName
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
    secretStoreName: keyVaultName
    serviceBusConnectionStringSecretName: servicebusConnectionStringSecretKeyName
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
    secretStoreName: keyVaultName
    serviceBusConnectionStringSecretName: servicebusConnectionStringSecretKeyName
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
    secretStoreName: keyVaultName
    serviceBusConnectionStringSecretName: servicebusConnectionStringSecretKeyName
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

module stateStore 'modules/dapr-component-statestore.bicep' = {
  name: 'cosmosDBStateStoreDeployment'
  params: {
    statestoreName: 'processedrequests'
    secretStoreName: keyVaultName
    cosmosDbUrl: cosmosDbUrl
    masterKey: cosmosDBKey
    databaseName: cosmosDBDatabaseName
    collectionName: 'statestore'
    environmentName: environmentName
    appScope: [
      '${BMSDAccountManagerServiceContainerAppName}'
    ]
  }
  dependsOn: [
    containersAppInfra
  ]
}

resource uamiResource 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' existing = {
  name: uamiName
}



resource BMSDCheckingAccountAccessorContainerApp 'Microsoft.App/containerApps@2022-10-01' = {
  name: BMSDCheckingAccountAccessorServiceContainerAppName
  tags: tags
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uamiResource.id}' : {}
    }
  }
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
            memory: '1.0Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://*:80'
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
    uami
    containersAppInfra
    servicebus
    keyvault
  ]
}


resource BMSDUserInfoAccessorContainerApp 'Microsoft.App/containerApps@2022-10-01' = {
  name: BMSDUserInfoAccessorServiceContainerAppName
  tags: tags
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uamiResource.id}' : {}
    }
  }
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
            memory: '1.0Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://*:80'
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
    keyvault
    uami
  ]
}

resource BMSDLiabilityValidatorEngineContainerApp 'Microsoft.App/containerApps@2022-10-01' = {
  name: BMSDLiabilityValidatorEngineServiceContainerAppName
  tags: tags
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uamiResource.id}' : {}
    }
  }
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
            memory: '1.0Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://*:80'
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
    keyvault
    uami
  ]
}


resource BMSDNotificationManagerContainerApp 'Microsoft.App/containerApps@2022-10-01' = {
  name: BMSDNotificationManagerServiceContainerAppName
  tags: tags
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uamiResource.id}' : {}
    }
  }
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
        {
          name: 'servicebuskeyref'
          value: serviceBusConnectionString
        }
        {
          name: 'signalrkeyref'
          value: signalRConnectionString
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
            memory: '1.0Gi'
          }         
          env: [
            {
              name: 'ASPNETCORE_URLS'
              value: 'http://*:80'
            }
 		        {
              name: 'AZURE__SignalR__ConnectionString'
              secretRef: 'signalrkeyref'
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
               queueName: 'clientresponsequeue'
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
        ] 
      }
    }
  }
  dependsOn: [
    containersAppInfra
    servicebus
    signalr
    keyvault
    uami
  ]
 }


resource BMSDAccountManagerContainerApp 'Microsoft.App/containerApps@2022-10-01' = {
  name: BMSDAccountManagerServiceContainerAppName
  tags: tags
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${uamiResource.id}' : {}
    }
  }
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      dapr: {
        enabled: true
        appPort: BMSDAccountManagerPort
        appId: BMSDAccountManagerServiceContainerAppName
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
        memory: '1.0Gi'
      }         
      env: [
      {
          name: 'ASPNETCORE_URLS'
          value: 'http://*:80'
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
    keyvault
    uami
  ]
}

output webServiceUrl string = BMSDAccountManagerContainerApp.properties.latestRevisionFqdn

//todo: add diagnostic setting to all resources: https://learn.microsoft.com/en-us/azure/azure-monitor/essentials/resource-manager-diagnostic-settings?tabs=bicep
        