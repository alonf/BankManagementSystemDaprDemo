using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;

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
            var container = _cosmosClient.GetContainer(_databaseName, AccountInfoName);
            var accountInfo = container.GetItemLinqQueryable<AccountInfo>(true)
                .Where(a => a.Id == accountId).AsEnumerable().FirstOrDefault();
               
            if (accountInfo != null)
                return accountInfo!;

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

                //Create a new AccountInfo document 
                var response = await container.CreateItemAsync(firstRecord);

                _logger.LogInformation(
                    $"GetAccountInfoAsync: create first account, result status: {response.StatusCode} ");
            }
            catch (CosmosException ex)
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

            accountInfo = container.GetItemLinqQueryable<AccountInfo>(true)
                 .Where(a => a.Id == accountId).AsEnumerable().FirstOrDefault();


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

                var container = _cosmosClient.GetContainer(_databaseName, AccountTransactionName);

                //create or update the record (if this is a retry operation)
                var response = await container.UpsertItemAsync(transactionRecord);

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

                //create an access condition based on etag
                var itemRequestOptions = new ItemRequestOptions()
                {
                    IfMatchEtag = accountInfo.ETag
                };

                var accountInfoContainer = _cosmosClient.GetContainer(_databaseName, AccountInfoName);
                
                //replace the account info document
                var replaceItemResponse = await accountInfoContainer.ReplaceItemAsync(accountInfo, accountInfo.Id, new PartitionKey(accountInfo.BankId), itemRequestOptions);

                _logger.LogInformation($"UpdateBalanceAsync: update account info, result status: {replaceItemResponse.StatusCode} ");
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

            //create the cosmos db database if not exist
            var response = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseName);
            _logger.LogInformation($"InitDBIfNotExistsAsync: CreateDatabaseIfNotExistsAsync status code:{response.StatusCode}");

            var database = _cosmosClient.GetDatabase(_databaseName);

            //create the account transaction collection if not exist
            var accountTransactionContainerResponse = database.CreateContainerIfNotExistsAsync(AccountTransactionName, "/accountId");
            _logger.LogInformation($"InitDBIfNotExistsAsync:  create {AccountTransactionName} if not exist status code:{accountTransactionContainerResponse.Status}");

            //create the account info collection if not exist
             var accountInfoContainerResponse = await database.CreateContainerIfNotExistsAsync(AccountInfoName, "/bankId");
            _logger.LogInformation($"InitDBIfNotExistsAsync: create {AccountInfoName} if not exist status code:{accountInfoContainerResponse.StatusCode}");
            
            _dbHasAlreadyInitiated = true;
        }

        public async Task<IList<AccountTransactionRecord>?> GetAccountTransactionHistoryAsync(string accountId, int numberOfTransactions)
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

                var container = _cosmosClient.GetContainer(_databaseName, AccountTransactionName);
                var transactionQuery = container.GetItemLinqQueryable<AccountTransactionRecord>()
                    .Where(r => transactionIds.Contains(r.Id)).ToFeedIterator();

                var accountTransactions = new List<AccountTransactionRecord>();
                double charge = 0.0;
                do
                {
                    var result = await transactionQuery.ReadNextAsync();
                    accountTransactions.AddRange(result);
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

                //create an access condition based on etag
                var itemRequestOptions = new ItemRequestOptions()
                {
                    IfMatchEtag = accountInfo.ETag
                };

                var container = _cosmosClient.GetContainer(_databaseName, AccountInfoName);
                var replaceItemResponse = await container.ReplaceItemAsync(accountInfo, accountInfo.Id, null, itemRequestOptions);
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
