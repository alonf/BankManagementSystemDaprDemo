param statestoreName string
param environmentName string
param cosmosDbUrl string
param masterKey string
param databaseName string
param collectionName string
param appScope array

resource daprComponentStateStore 'Microsoft.App/managedEnvironments/daprComponents@2022-03-01' = {
  name: '${environmentName}/${statestoreName}'
  properties: {
    componentType: 'state.azure.cosmosdb'
    version: 'v1'
    metadata: [
      {
        name: 'url'
        value: cosmosDbUrl
      }
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
  
 
