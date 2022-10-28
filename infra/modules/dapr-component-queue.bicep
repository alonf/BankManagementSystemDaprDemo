
param environmentName string
param serviceBusConnectionStringSecretName string
param queueName string
param appScope array
@secure()
param secretStoreName string


resource daprComponentQueue 'Microsoft.App/managedEnvironments/daprComponents@2022-06-01-preview' = {
  name: '${environmentName}/${queueName}'
  properties: {
    componentType: 'bindings.azure.servicebusqueues'
    version: 'v1'
    metadata: [
      {
        name: 'connectionString'
        secretRef: serviceBusConnectionStringSecretName
      }
      {
        name: 'queueName'
        value: queueName
      }
      {
        name: 'ttlInSeconds'
        value: '60'
      }
    ]
    secretStoreComponent: secretStoreName
    // Application scopes
    scopes: appScope
  }
}
