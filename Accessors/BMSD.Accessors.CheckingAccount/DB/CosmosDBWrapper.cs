using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using BMSD.Accessors.CheckingAccount.DB;
using Microsoft.Azure.Cosmos;


namespace BMSD.Accessors.CheckingAccount.DB
{

    public class CosmosDBWrapper : ICosmosDBWrapper
    {
        private readonly CosmosClient _cosmosClient;
        private readonly string _databaseName;
        private readonly ILogger _logger;
        private bool _dbHasAlreadyInitiated;
        private const string AccountInfoName = "AccountInfo";
        private const string AccountTransactionName = "AccountTransaction";
        
        public CosmosDBWrapper(CosmosClient cosmosClient, string databaseName, ILogger logger)
        {
            _cosmosClient = cosmosClient;
            _databaseName = databaseName;
            _logger = logger;
        }

        public async Task<AccountInfo> GetAccountInfoAsync(string accountId)
        {
            await InitDBIfNotExistsAsync();

            //find the account info document
            Uri accountInfoCollectionUri = UriFactory.CreateDocumentCollectionUri(_databaseName, AccountInfoName);
            var accountInfoQuery = _cosmosClient
                .CreateDocumentQuery<AccountInfo>(accountInfoCollectionUri,
                    new FeedOptions { EnableCrossPartitionQuery = true }).Where(r => r.Id == accountId)
                .AsDocumentQuery();
            var accountInfo = (await accountInfoQuery.ExecuteNextAsync<AccountInfo>()).FirstOrDefault();

            
            if (accountInfo != null)
                return accountInfo;

            //else, first record for the account

            var firstRecord = new AccountInfo()
            {
                Id = accountId,
                AccountBalance = 0,
                OverdraftLimit = 1000 //default
            };

            try
            {
                //in a case of race condition, only a single document is created (id)
                var response = await _cosmosClient.CreateDocumentAsync(accountInfoCollectionUri, firstRecord);

                _logger.LogInformation(
                    $"GetAccountInfoAsync: create first account, result status: {response.StatusCode} ");
            }
            catch (DocumentClientException ex)
            {
                if (ex.StatusCode != System.Net.HttpStatusCode.Conflict)
                {
                    _logger.LogInformation(
                        "GetAccountInfoAsync: multiple concurrent attempts to initialize the account info record detected");
                }
                else
                {
                    _logger.LogError(
                        $"GetAccountInfoAsync: Error creating or querying account document. Exception: {ex}");
                    throw;
                }
            }

            accountInfoQuery = _cosmosClient
                .CreateDocumentQuery<AccountInfo>(accountInfoCollectionUri,
                    new FeedOptions { EnableCrossPartitionQuery = true }).Where(r => r.Id == accountId)
                .AsDocumentQuery();
            accountInfo = (await accountInfoQuery.ExecuteNextAsync<AccountInfo>()).FirstOrDefault();

            if (accountInfo != null) 
                return accountInfo;

            //else
            _logger.LogError("GetAccountInfoAsync: Error creating or querying account document");
            throw new Exception("Error creating account document");
        }

        public async Task UpdateBalanceAsync(string requestId, string accountId, decimal amount)
        {
            try
            {
                var transactionRecord = new AccountTransactionRecord()
                {
                    Id = requestId,
                    AccountId = accountId,
                    TransactionAmount = amount,
                    TransactionTime = DateTimeOffset.UtcNow
                };

                await InitDBIfNotExistsAsync();

                Uri accountTransactionCollectionUri = UriFactory.CreateDocumentCollectionUri(_databaseName, "AccountTransaction");

                //create or update the record (if this is a retry operation)
                var response = await _cosmosClient.UpsertDocumentAsync(accountTransactionCollectionUri, transactionRecord);
                _logger.LogInformation($"UpdateBalanceAsync: insert transaction to collection, result status: {response.StatusCode} ");

                //find the account info document
                var accountInfo = await GetAccountInfoAsync(accountId);

                //check if already processed
                if (accountInfo.AccountTransactions.Contains(transactionRecord.Id))
                {
                    _logger.LogError($"UpdateBalanceAsync: {transactionRecord.Id} already processed");
                    return;
                }
                accountInfo.AccountTransactions.Add(transactionRecord.Id);
                accountInfo.AccountBalance += transactionRecord.TransactionAmount;

                var ac = new AccessCondition { Condition = accountInfo.ETag, Type = AccessConditionType.IfMatch };

                //throws exception on concurrency conflicts => retry
                await _cosmosClient.ReplaceDocumentAsync(accountInfo.Self, accountInfo,
                    new RequestOptions { AccessCondition = ac });
            }
            catch (Exception ex)
            {
                _logger.LogError($"UpdateBalanceAsync: exception: {ex}");
                throw;
            }
        }


        private async Task InitDBIfNotExistsAsync()
        {
            //to save some cycles when the function continue to run, this code can run concurrently
            if (_dbHasAlreadyInitiated)
                return;

            var resourceResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(new Database { Id = _databaseName });
            _logger.LogInformation($"InitDBIfNotExistsAsync: CreateDatabaseIfNotExistsAsync status code:{resourceResponse.StatusCode}");
            
            Uri databaseUri = UriFactory.CreateDatabaseUri(_databaseName);
            var documentCollection = new DocumentCollection
            {
                Id = AccountTransactionName,
                PartitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string> { "/requestId" }
                }
            };

            //create collection if not exist
            var documentResourceResponse = await _cosmosClient.CreateDocumentCollectionIfNotExistsAsync(databaseUri, documentCollection);
            _logger.LogInformation($"InitDBIfNotExistsAsync: CreateDocumentCollectionIfNotExistsAsync for {AccountTransactionName} returned status code:{documentResourceResponse.StatusCode}");

            documentCollection = new DocumentCollection
            {
                Id = AccountInfoName,
                PartitionKey = new PartitionKeyDefinition
                {
                    Paths = new Collection<string> { "/overdraftLimit" }
                }
            };

            //create collection if not exist
            documentResourceResponse = await _cosmosClient.CreateDocumentCollectionIfNotExistsAsync(databaseUri, documentCollection);
            _logger.LogInformation($"InitDBIfNotExistsAsync: CreateDocumentCollectionIfNotExistsAsync for {AccountInfoName} returned status code:{documentResourceResponse.StatusCode}");

            _dbHasAlreadyInitiated = true;
        }

        public async Task<IList<AccountTransactionRecord>> GetAccountTransactionHistoryAsync(string accountId, int numberOfTransactions)
        {
            try
            {
                //get the account record
                var accountInfo = await GetAccountInfoAsync(accountId);

                if (accountInfo == null)
                {
                    _logger.LogWarning($"GetAccountTransactionHistoryAsync: account id: {accountId} not found");
                    return null;
                }

                var transactionIds = accountInfo.AccountTransactions.Skip(Math.Max(accountInfo.AccountTransactions.Count - numberOfTransactions, 0));

                Uri accountTransactionsCollectionUri = UriFactory.CreateDocumentCollectionUri(_databaseName, AccountTransactionName);

                var transactionQuery = _cosmosClient
                    .CreateDocumentQuery<AccountTransactionRecord>(accountTransactionsCollectionUri,
                        new FeedOptions { EnableCrossPartitionQuery = true }).Where(r => transactionIds.Contains(r.Id)).AsDocumentQuery();

                var accountTransactions = new List<AccountTransactionRecord>();
                double charge = 0.0;
                do
                {
                    var result = await transactionQuery.ExecuteNextAsync<AccountTransactionRecord>();
                    accountTransactions.AddRange(result.ToArray());
                    charge += result.RequestCharge;
                } while (transactionQuery.HasMoreResults);

                _logger.LogInformation($"GetAccountTransactionHistoryAsync: Querying for transactions returned {accountTransactions.Count} transactions and cost {charge} RU");

                return accountTransactions;
            }
            catch(Exception ex)
            {
                _logger.LogError($"GetAccountTransactionHistoryAsync: exception: {ex}");
                throw;
            }
        }

        public async Task<decimal> GetBalanceAsync(string accountId)
        {
            //get the account record
            AccountInfo accountInfo = await GetAccountInfoAsync(accountId);

            if (accountInfo == null)
            {
                _logger.LogWarning($"GetBalanceAsync: account id: {accountId} is not found");
                throw new KeyNotFoundException();
            }

            return accountInfo.AccountBalance;
        }

        public async Task SetAccountBalanceLowLimitAsync(string accountId, decimal limit)
        {
            try
            {
                //get the account record
                AccountInfo accountInfo = await GetAccountInfoAsync(accountId);
                
                if (accountInfo == null)
                {
                    _logger.LogWarning($"GetBalanceAsync: account id: {accountId} is not found");
                    throw new KeyNotFoundException();
                }

                accountInfo.OverdraftLimit = limit;

                var ac = new AccessCondition { Condition = accountInfo.ETag, Type = AccessConditionType.IfMatch };

                //throws exception on concurrency conflicts => retry
                await _cosmosClient.ReplaceDocumentAsync(accountInfo.Self, accountInfo,
                    new RequestOptions { AccessCondition = ac });
            }
            catch (Exception ex)
            {
                _logger.LogError($"SetAccountBalanceLowLimit: exception: {ex}");
                throw;
            }
        }

        public async Task<decimal> GetAccountBalanceLowLimitAsync(string accountId)
        {
            //get the account record
            AccountInfo accountInfo = await GetAccountInfoAsync(accountId);
            
            if (accountInfo == null)
            {
                _logger.LogWarning($"GetAccountBalanceLowLimit: account id: {accountId} is not found");
                throw new KeyNotFoundException();
            }

            return accountInfo.OverdraftLimit;
        }

        
    }
}
