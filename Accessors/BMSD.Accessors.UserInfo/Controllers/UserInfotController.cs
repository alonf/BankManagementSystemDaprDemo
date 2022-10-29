using BMS.Accessors.UserInfo;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using System.Net;
using System.Text.Json.Nodes;
using NJsonSchema;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BMSD.Accessors.UserInfo.Controllers
{

    [ApiController]
    [Route("[controller]")]
    public class UserInfotController : ControllerBase
    {
        private static bool _dbHasAlreadyInitiated;
        const string DatabaseName = "BMSDB";
        const string CollectionName = "UserInfo";
        
        private readonly ILogger<UserInfotController> _logger;
        private readonly DaprClient _daprClient;
        private readonly CosmosClient _cosmosClient;

        public UserInfotController(ILogger<UserInfotController> logger, DaprClient daprClient, 
            IConfiguration configuration, CosmosClient cosmosClient)
        {
            _logger = logger;
            _daprClient = daprClient;
            _cosmosClient = cosmosClient;
        }
      

        [HttpPost("/customerregistrationqueue")]
        public async Task<IActionResult> HandleCustomerRegistrationRequestsAsync()
        {
            string userAccountId = "unknown";
            string requestId = "unknown";
            string callerId = "unknown";
            try
            {
                //get Jobject from the request body
                var myQueueItem = await new StreamReader(Request.Body).ReadToEndAsync();
                _logger.LogInformation($"RegisterCustomer: Queue processed: {myQueueItem}");
                await ValidateInputAsync(myQueueItem);

                var customerRegistrationInfo = JsonNode.Parse(myQueueItem);
                if (customerRegistrationInfo == null)
                {
                    _logger.LogError("RegisterCustomer: Error: invalid customer call: " + myQueueItem);
                    return Problem("RegisterCustomer: Error: invalid customer call");
                }

                userAccountId = customerRegistrationInfo["accountId"]!.ToString();
                customerRegistrationInfo["id"] = userAccountId;
                customerRegistrationInfo.AsObject().Remove("accountId");

                //get the requestId from the customerRegistrationInfo
                requestId = customerRegistrationInfo["requestId"]!.ToString();

                //get the callerId from the customerRegistrationInfo
                callerId = customerRegistrationInfo["callerId"]!.ToString();

                //create db if not exist
                await InitDBIfNotExistsAsync();

                var container = _cosmosClient.GetContainer(DatabaseName, CollectionName);

                //Check if a customer with the user account id is already exist in db
                var sql = "SELECT * FROM c WHERE c.id = @id";
                var sqlQuery = new QueryDefinition(sql).WithParameter("@id", userAccountId);
                var iterator = container.GetItemQueryIterator<JsonNode>(sqlQuery);
                var result = await iterator.ReadNextAsync();
                if (result.Count > 0)
                {
                    await EnqueueResponseMessageAsync("RegisterCustomer",
                    true, "Customer already exist",
                        requestId,
                        callerId,
                        userAccountId);

                    _logger.LogInformation($"RegisterCustomer: User account id: {userAccountId} already exist");
                    return Ok();
                }

                //it will throw if Item exist in case of a race condition
                //create new document
                var response = await container.CreateItemAsync(customerRegistrationInfo);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(1000);
                    _logger.LogError("RegisterCustomer: Too many requests");
                    throw new Exception("Too many requests, try again");
                }

                if (IsSuccessStatusCode(response.StatusCode))
                {
                    await EnqueueResponseMessageAsync("RegisterCustomer",
                    true, "Customer registered successfully",
                        requestId,
                        callerId,
                        userAccountId);
                    _logger.LogInformation("RegisterCustomer: New account created");
                    return Ok();
                }
                //else
                await EnqueueResponseMessageAsync("RegisterCustomer",
                false, "Customer registered failed, retrying",
                    requestId,
                    callerId,
                    userAccountId);
                _logger.LogError($"RegisterCustomer: account creation failed with status code: {response.StatusCode}");
                throw new Exception("RegisterCustomer: account creation failed");
            }
            catch (JSchemaValidationException schemaValidationException)
            {
                _logger.LogError($"Json validation error on queued message: {schemaValidationException.Message}");
                await EnqueueResponseMessageAsync("RegisterCustomer",
                    false, "Customer registered failed, message format incorrect",
                    requestId,
                    callerId,
                    userAccountId);
                return Ok("Json validation error on queued message");
            }
            catch (CosmosException ex)
            {
                _logger.LogError($"RegisterCustomer: DocumentClientException when accessing cosmosDB: {ex}");
                if (ex.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    await Task.Delay(1000);
                    throw; //retry
                }
                await EnqueueResponseMessageAsync("RegisterCustomer",
                    false, "Customer registered failed, Database access error. Retrying",
                requestId,
                callerId,
                    userAccountId);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError($"RegisterCustomer: A problem occur, exception: {ex}");
                await EnqueueResponseMessageAsync("RegisterCustomer",
                    false, "Customer registered failed, unknown server error. Retrying",
                    requestId,
                    callerId,
                    userAccountId);
                throw; //retry
            }
        }

        [HttpGet("/GetAccountIdByEmail")]
        public async Task<IActionResult> GetAccountIdByEmailAsync([FromQuery] string email)
        {
            try
            {
                _logger.LogInformation("GetAccountIdByEmail HTTP processed a request.");

                //create db if not exist
                await InitDBIfNotExistsAsync();
                var container = _cosmosClient.GetContainer(DatabaseName, CollectionName);

                var sql = "SELECT c.id FROM c WHERE c.email = @email";
                var sqlQuery = new QueryDefinition(sql).WithParameter("@email", email);
                var iterator = container.GetItemQueryIterator<JsonObject>(sqlQuery);

                var accountIds = new List<string>();
                double charge = 0.0;
                do
                {
                    var result = await iterator.ReadNextAsync();
                    var ids = result.Select(r => "\"" + JsonObject.Parse(r.ToString())!["id"]!.ToString() + "\"").Cast<string>();
                    accountIds.AddRange(ids);
                    charge += result.RequestCharge;
                } while (iterator.HasMoreResults);

                _logger.LogInformation($"GetAccountIdByEmail: Querying for account ids returned {accountIds.Count} accounts and cost {charge} RUs");

                var resultJson = $"{{\"accountIds\":[{String.Join(',', accountIds)}]}}";
                return Ok(JsonNode.Parse(resultJson));
            }
            catch (Exception ex)
            {
                _logger.LogError($"GetAccountIdByEmailAsync: A problem occur, exception: {ex}");
                return Problem("GetAccountIdByEmailAsync: A problem occur, exception: " + ex.Message);
            }
        }


        private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
        {
            return ((int)statusCode >= 200) && ((int)statusCode <= 299);
        }
        
        private static async Task ValidateInputAsync(string customerRegistrationInfo)
        {
            string schemaJson = @"{
                  '$schema' : 'https://json-schema.org/draft/2020-12/schema',
                  'description': 'a user information creation request',
                  'title': 'UserInfo',
                  'type': 'object',
                  'properties': {
                    'requestId': {'type': 'string'},
                    'accountId': {'type': 'string'},
                    'fullName': {'type': 'string'},
                    'email': {
                        'type': 'string',
                        'pattern': '^\\S+@\\S+\\.\\S+$',
                        'format': 'email',
                        'minLength': 6,
                        'maxLength': 127
                    }
                  },
                    'required' : ['requestId', 'accountId', 'fullName', 'email']
                }"
            ;


            JsonSchema schema = await JsonSchema.FromJsonAsync(schemaJson);
            var validationResult = schema.Validate(customerRegistrationInfo);
            if (validationResult.Any())
            {
                throw new JSchemaValidationException(validationResult);
            }
        }

        private async Task InitDBIfNotExistsAsync()
        {
            //to save some cycles when the function continue to run, this code can run concurrently
            if (_dbHasAlreadyInitiated)
                return;

            //create the cosmos db database if not exist
            var response = await _cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
            _logger.LogInformation($"InitDBIfNotExistsAsync: CreateDatabaseIfNotExistsAsync status code:{response.StatusCode}");

            var database = _cosmosClient.GetDatabase(DatabaseName);

            //create the user info collection if not exist
            var userInfoContainerResponse = database.CreateContainerIfNotExistsAsync(CollectionName, "/email");
            _logger.LogInformation($"InitDBIfNotExistsAsync:  create {CollectionName} if not exist status code:{userInfoContainerResponse.Status}");

            _dbHasAlreadyInitiated = true;
        }

        private async Task EnqueueResponseMessageAsync(string actionName, bool isSuccessful,
            string resultMessage, string requestId, string callerId, string accountId = "")
        {
            var responseMessage = new JsonObject
            {
                ["actionName"] = actionName,
                ["isSuccessful"] = isSuccessful,
                ["resultMessage"] = resultMessage,
                ["requestId"] = requestId,
                ["callerId"] = callerId,
                ["accountId"] = accountId
            };

            //use Dapr to enque the message
            await _daprClient.InvokeBindingAsync("clientresponsequeue", "create", responseMessage);

        }
    }
}
