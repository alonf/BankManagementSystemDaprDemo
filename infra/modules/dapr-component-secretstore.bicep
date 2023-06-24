
param keyvaultName string
param environmentName string
param appScope array
param clientId string


resource kvSecretStoreComponent 'Microsoft.App/managedEnvironments/daprComponents@2022-10-01' = {
  name: '${environmentName}/${keyvaultName}'
  properties: {
    componentType: 'secretstores.azure.keyvault'
    version: 'v1'
    ignoreErrors: false
    initTimeout: '5s'    
    metadata: [
      {
        name: 'vaultName'
        value: keyvaultName
      }
      {
        name: 'azureClientId'
        value: clientId
      }
    ]
    scopes: appScope
  }
} 