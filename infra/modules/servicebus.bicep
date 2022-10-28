param skuName string = 'Standard'
param location string
param keyvaultName string 
param serviceBusConnectionStringSecretName string
param servicebusNamespaceName string
param objectId string

param queueNames array = [
  'accounttransactionqueue'
  'clientresponsequeue'
  'customerregistrationqueue'
]

var deadLetterFirehoseQueueName = 'deadletterfirehose'

resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2021-11-01' = {
  name: servicebusNamespaceName
  location: location
  sku: {
    name: skuName
    tier: skuName
  }
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${objectId}' : {}
    }
  }
}

resource deadLetterFirehoseQueue 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = {
  name: deadLetterFirehoseQueueName
  parent: serviceBusNamespace
  properties: {
    requiresDuplicateDetection: false
    requiresSession: false
    enablePartitioning: false
  }
}

resource queues 'Microsoft.ServiceBus/namespaces/queues@2021-11-01' = [for queueName in queueNames: {
  parent: serviceBusNamespace
  name: queueName
  dependsOn: [
    deadLetterFirehoseQueue
  ]
  properties: {
    forwardDeadLetteredMessagesTo: deadLetterFirehoseQueueName
  }
}]


var serviceBusEndpoint = '${serviceBusNamespace.id}/AuthorizationRules/RootManageSharedAccessKey'

resource connection_string_secret 'Microsoft.KeyVault/vaults/secrets@2021-11-01-preview' = {
  name: '${keyvaultName}/${serviceBusConnectionStringSecretName}'
  properties: {
    contentType: 'text/plain'
    value: listKeys(serviceBusEndpoint, serviceBusNamespace.apiVersion).primaryConnectionString
  }
}
