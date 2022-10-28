param signalRName string
param location string
param objectId string
param keyvaultName string 
param signalRConnectionStringSecretName string

resource signalR 'Microsoft.SignalRService/signalR@2022-02-01' = {
  name: signalRName
  location: location
  sku: {
    name: 'Standard_S1'
    capacity: 1
  }
  kind: 'SignalR'
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${objectId}' : {}
    }
  }
  properties: {
    tls: {
      clientCertEnabled: false
    }
    features: [
      {
        flag: 'ServiceMode'
        value: 'Serverless'
      }
      {
        flag: 'EnableConnectivityLogs'
        value: 'true'
      }
      {
        flag: 'EnableMessagingLogs'
        value: 'true'
      }
      {
        flag: 'EnableLiveTrace'
        value: 'true'
      }
    ]
    cors: {
        allowedOrigins: [
          '*'
        ]
    }
    
    networkACLs: {
      defaultAction: 'Deny'
      publicNetwork: {
        allow: [
          'ClientConnection'
          'ServerConnection'
          'RESTAPI'
          'Trace'
        ]
      }
      /*privateEndpoints: [
        {
          name: 'mySignalRService.1fa229cd-bf3f-47f0-8c49-afb36723997e'
          allow: [
            'ServerConnection'
          ]
        }
      ]*/
    }
    /*
    upstream: {
      templates: [
        {
          categoryPattern: '*'
          eventPattern: 'connect,disconnect'
          hubPattern: '*'
          urlTemplate: 'https://bmsd.com/accountmanagercallback/api/connect'
        }
      ]
    }*/
  }
}

var key = signalR.listKeys().primaryConnectionString

resource connection_string_secret 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  name: '${keyvaultName}/${signalRConnectionStringSecretName}'
  properties: {
    contentType: 'text/plain'
    value: key
  }
}

