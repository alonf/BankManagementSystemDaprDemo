param keyVaultName string
param location string
param objectId string


resource keyvault 'Microsoft.KeyVault/vaults@2021-11-01-preview' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    enableSoftDelete: false
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    tenantId: tenant().tenantId
     accessPolicies: [
      {
        objectId: objectId
        tenantId: tenant().tenantId
        permissions: {
          keys: [
          'get'
          'list'
          ]
          secrets: [
              'get'
              'list'
          ]
        }
      }
    ]
  }
}

var key = keyvault.listKeys().primaryConnectionString
output keyvaultConnectionString string = key
