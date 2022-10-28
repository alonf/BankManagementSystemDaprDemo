
param environmentName string
param serviceBusConnectionStringSecretName string
param queueName string
param appScope array


resource daprComponentQueue 'Microsoft.App/managedEnvironments/daprComponents@2022-03-01' = {
  name: '${environmentName}/${queueName}'
  properties: {
    componentType: 'bindings.azure.servicebusqueues'
    version: 'v1'
    secrets: [
      {
        name: 'servicebuskeyref'
        value: serviceBusConnectionStringSecretName
      }
    ]
    metadata: [
      {
        name: 'connectionString'
        secretRef: 'servicebuskeyref'
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
    // Application scopes
    scopes: appScope
  }
}
