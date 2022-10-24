param resourceName string
param resourceAppName string
param tags object
param location string
param environmentId string
param appPort int
param skuName string = 'Standard'
param location string
param secrets array
param containerRegistry string
param containerRegistryUsername string
param ingress object
param image string
param env array
param dependsOn array

resource resourceName 'Microsoft.App/containerApps@2022-03-01' = {
  name: resourceAppName
  tags: tags
  location: location
  properties: {
    managedEnvironmentId: environmentId
    configuration: {
      dapr: {
        enabled: true
        appPort: appPort
        appId: resourceAppName
        appProtocol: 'http'
      }
      secrets: secrets
      registries: [
        {
          server: containerRegistry
          username: containerRegistryUsername
          passwordSecretRef: 'container-registry-password-ref'
        }
      ]
      ingress: ingress
      probes: [
        {
          type: 'liveness'
          failureThreshold: 3
          periodSeconds: 10
          initialDelaySeconds: 15
          successThreshold: 1
          tcpSocket: {
            port: 80
          }
          timeoutSeconds: 1
        }
        {
          type: 'readiness'
          failureThreshold: 48
          initialDelaySeconds: 15
          periodSeconds: 5
          successThreshold: 1
          tcpSocket: {
             port: 80
          }
             timeoutSeconds: 5
        }
      ]
    }
    template: {
      containers: [
        {
          image: '${containerRegistry}/${image}'
          name: resourceAppName
          resources: {
            cpu: 0.5
            memory: '1.0Gi'
          }
          env: env
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
  dependsOn: dependsOn
}
output webServiceUrl string = BMSDAccountManagerContainerApp.properties.latestRevisionFqdn


