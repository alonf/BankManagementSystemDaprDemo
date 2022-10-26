param environmentName string
param signalrKey string
param signalRName string
param appScope array

resource daprComponentSignalR 'Microsoft.App/managedEnvironments/daprComponents@2022-06-01-preview' = {
  name: '${environmentName}/${signalRName}'
  properties: {
    componentType: 'bindings.azure.signalr'
    version: 'v1'
    secrets: [
      {
        name: 'signalrkeyref'
        value:  signalrKey
      }
    ]
    metadata: [
      {
        name: 'connectionString'
        secretRef: 'signalrkeyref'
      }
      {
        name: 'hub'
        value: 'accountmanagercallback'
      }
    ]
    // Application scopes
    scopes: appScope
  }
}

