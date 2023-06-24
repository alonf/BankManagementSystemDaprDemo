param keyVaultName string
param location string
param objectId string
param tenantId string

resource keyvault 'Microsoft.KeyVault/vaults@2023-02-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    enabledForDeployment: true
    enabledForTemplateDeployment: true
    enableSoftDelete: false
    tenantId: tenant().tenantId
     accessPolicies: [
      {
        objectId: objectId
        tenantId: tenantId
        permissions: {
          keys: [
          'all'
          ]
          secrets: [
              'all'
          ]
        }
      }
    ]
  }
}

//var key = keyvault.listKeys().primaryConnectionString
//output keyvaultConnectionString string = key
