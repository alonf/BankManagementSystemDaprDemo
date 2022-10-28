param statestoreName string
param environmentName string
param cosmosDbUrl string
param masterKey string
param databaseName string
param collectionName string
param appScope array
@secure()
param secretStoreName string

resource daprComponentStateStore 'Microsoft.App/managedEnvironments/daprComponents@2022-06-01-preview' = {
  name: '${environmentName}/${statestoreName}'
  properties: {
    componentType: 'state.azure.cosmosdb'
    version: 'v1'
    ignoreErrors: true
    initTimeout: '5m'
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
      {
       name: 'actorStateStore'
       value: 'false'
      }
    ]
    secretStoreComponent: secretStoreName
    // Application scopes
    scopes: appScope
  }
}
  
 
