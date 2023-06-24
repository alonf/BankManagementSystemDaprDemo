@description('Specifies the name of the user assigned managed identity.')
param uamiName string
param location string = resourceGroup().location

resource uami 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: uamiName
  location: location
}

output principalId string = uami.properties.principalId
output tenantId string = uami.properties.tenantId
output clientId string = uami.properties.clientId
output uamiId string = uami.id
