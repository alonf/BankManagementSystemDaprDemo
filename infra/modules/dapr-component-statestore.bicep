param environmentName string
param masterKey string
param databaseName string
param collectionName string
param appScope array

resource daprComponentStateStore 'Microsoft.App/managedEnvironments/daprComponents@2022-03-01' = {
  name: '${environmentName}/statestore'
  properties: {
    componentType: 'state.azure.cosmosdb'
    version: 'v1'
    metadata: [
      {
        name: 'masterKey'
        value: masterKey
      }
	  {
	    name: 'database'
		value:  databaseName
      }
	  {
	    name: 'collection'
	    value:  collectionName
      }  
    ]
    // Application scopes
    scopes: appScope
  }
}
  
 
