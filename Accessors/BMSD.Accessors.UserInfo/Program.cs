using Dapr.Client;
using Google.Protobuf.Reflection;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using System.Net;
using System.Text.Json.Nodes;
using Json.Schema;
using Newtonsoft.Json.Linq;
using Json.More;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Newtonsoft.Json;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;

namespace BMS.Accessors.UserInfo
{
    public class Program
    {
        private static bool _dbHasAlreadyInitiated;
        static string? DatabaseName { get; set; }
        const string CollectionName = "UserInfo";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            builder.Services.AddControllers().AddDapr();
            
            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();

            //get the cosmos db connection string from the configuration
            var cosmosDbConnectionString = builder.Configuration["CosmosDbConnectionString"];

            //check that cosmosDbConnectionString is not null
            if (string.IsNullOrWhiteSpace(cosmosDbConnectionString))
            {
                throw new Exception("Error in configuration: CosmosDbConnectionString is null or empty");
            }

            //get cosmos db checking account database name from configuration
            var DatabaseName = builder.Configuration["CosmosDbUserInfoDatabaseName"];

            //check that the database name is not null
            if (string.IsNullOrWhiteSpace(DatabaseName))
            {
                throw new Exception("Error in configuration: CosmosDbUserInfoDatabaseName is null or empty");
            }

            //Create Cosmos db client using cosmos client builder and camel case serializer
            var cosmosClient = new CosmosClientBuilder(cosmosDbConnectionString)
                .WithSerializerOptions(new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                })
                .Build();


            //Register Customer
            app.MapPost("/customerregistrationqueue", async (HttpContext httpContext,
                    DaprClient daprClient, ILogger logger) =>
            {
                string userAccountId = "unknown";
                string requestId = "unknown";
                string callerId = "unknown";
                try
                {
                    //get Jobject from the request body
                    var myQueueItem = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                    logger.LogInformation($"RegisterCustomer: Queue processed: {myQueueItem}");

                    var customerRegistrationInfo = JsonObject.Parse(myQueueItem)?.AsObject();
                    if (customerRegistrationInfo == null)
                    {
                        logger.LogError("RegisterCustomer: Error: invalid customer call: " + myQueueItem);
                        return Results.Problem("RegisterCustomer: Error: invalid customer call");
                    }
                    ValidateInput(customerRegistrationInfo);

                    userAccountId = customerRegistrationInfo["accountId"]!.ToString();
                    customerRegistrationInfo["id"] = userAccountId;
                    customerRegistrationInfo.Remove("accountId");

                    //get the requestId from the customerRegistrationInfo
                    requestId = customerRegistrationInfo["requestId"]!.ToString();

                    //get the callerId from the customerRegistrationInfo
                    callerId = customerRegistrationInfo["callerId"]!.ToString();
                    
                    //create db if not exist
                    await InitDBIfNotExistsAsync(cosmosClient, logger);

                    var container = cosmosClient.GetContainer(DatabaseName, CollectionName);

                    //Check if a customer with the user account id is already exist in db
                    var sql = "SELECT * FROM c WHERE c.id = @id";
                    var sqlQuery = new QueryDefinition(sql).WithParameter("@id", userAccountId);
                    var iterator = container.GetItemQueryIterator<JsonObject>(sqlQuery);
                    var result = await iterator.ReadNextAsync();
                    if (result.Count > 0)
                    {
                        await EnqueueResponseMessageAsync(daprClient, "RegisterCustomer",
                            true, "Customer already exist",
                            requestId,
                            callerId,
                            userAccountId);

                        logger.LogInformation($"RegisterCustomer: User account id: {userAccountId} already exist");
                        return Results.Ok();
                    }

                    //it will throw if Item exist in case of a race condition
                    //create new document
                    var response = await container.CreateItemAsync(customerRegistrationInfo);
                    
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        await Task.Delay(1000);
                        logger.LogError("RegisterCustomer: Too many requests");
                        throw new Exception("Too many requests, try again");
                    }

                    if (IsSuccessStatusCode(response.StatusCode))
                    {
                        await EnqueueResponseMessageAsync(daprClient, "RegisterCustomer",
                            true, "Customer registered successfully",
                            requestId,
                            callerId,
                            userAccountId);
                        logger.LogInformation("RegisterCustomer: New account created");
                        return Results.Ok();
                    }
                    //else
                    await EnqueueResponseMessageAsync(daprClient, "RegisterCustomer",
                        false, "Customer registered failed, retrying",
                        requestId,
                        callerId,
                        userAccountId);
                    logger.LogError($"RegisterCustomer: account creation failed with status code: {response.StatusCode}");
                    throw new Exception("RegisterCustomer: account creation failed");
                }
                catch (JSchemaValidationException schemaValidationException)
                {
                    logger.LogError($"Json validation error on queued message: {schemaValidationException.Message}");
                    await EnqueueResponseMessageAsync(daprClient, "RegisterCustomer",
                        false, "Customer registered failed, message format incorrect",
                        requestId,
                        callerId,
                        userAccountId);
                    return Results.Ok("Json validation error on queued message");
                }
                catch (CosmosException ex)
                {
                    logger.LogError($"RegisterCustomer: DocumentClientException when accessing cosmosDB: {ex}");
                    if (ex.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        await Task.Delay(1000);
                        throw; //retry
                    }
                    await EnqueueResponseMessageAsync(daprClient, "RegisterCustomer",
                        false, "Customer registered failed, Database access error. Retrying",
                        requestId,
                        callerId,
                        userAccountId);
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError($"RegisterCustomer: A problem occur, exception: {ex}");
                    await EnqueueResponseMessageAsync(daprClient, "RegisterCustomer",
                        false, "Customer registered failed, unknown server error. Retrying",
                        requestId,
                        callerId,
                        userAccountId);
                    throw; //retry
                }
            });

            app.MapGet("/GetAccountIdByEmail", async (HttpContext httpContext,
                    DaprClient daprClient, ILogger logger) =>
            {
                try
                {
                    logger.LogInformation("GetAccountIdByEmail HTTP processed a request.");

                    string email = httpContext.Request.Query["email"];

                    if (string.IsNullOrEmpty(email))
                    {
                        logger.LogError("GetAccountIdByEmailAsync: email is empty");
                        return Results.Problem("email parameter is missing");
                    }

                    //create db if not exist
                    await InitDBIfNotExistsAsync(cosmosClient, logger);
                    var container = cosmosClient.GetContainer(DatabaseName, CollectionName);
                    
                    var sql = "SELECT c.id FROM c WHERE c.email = @email";
                    var sqlQuery = new QueryDefinition(sql).WithParameter("@email", email);
                    var iterator = container.GetItemQueryIterator<JsonObject>(sqlQuery);
                                       
                    var accountIds = new List<string>();
                    double charge = 0.0;
                    do
                    {
                        var result = await iterator.ReadNextAsync();
                        var ids = result.Select(r => "'" + JsonObject.Parse(r.ToString())!["id"]!.ToString() + "'").Cast<string>();
                        accountIds.AddRange(ids);
                        charge += result.RequestCharge;
                    } while (iterator.HasMoreResults);

                    logger.LogInformation($"GetAccountIdByEmail: Querying for account ids returned {accountIds.Count} accounts and cost {charge} RUs");

                    return Results.Ok(JsonObject.Parse($"{{'accountIds':[{String.Join(',', accountIds)}]}}"));
                }
                catch (Exception ex)
                {
                    logger.LogError($"GetAccountIdByEmailAsync: A problem occur, exception: {ex}");
                    return Results.Problem("GetAccountIdByEmailAsync: A problem occur, exception: " + ex.Message);
                }
            });

            app.Run();
        }

        private static bool IsSuccessStatusCode(HttpStatusCode statusCode)
        {
            return ((int)statusCode >= 200) && ((int)statusCode <= 299);
        }

        private static void ValidateInput(JsonNode customerRegistrationInfo)
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

            JsonSchema schema = JsonSchema.FromText(schemaJson);
            var validationResult = schema.Validate(customerRegistrationInfo);
            if (!validationResult.IsValid)
            {
                throw new JSchemaValidationException(validationResult);
            }
        }

        private static async Task InitDBIfNotExistsAsync(CosmosClient cosmosClient, ILogger logger)
        {
            //to save some cycles when the function continue to run, this code can run concurrently
            if (_dbHasAlreadyInitiated)
                return;

            //create the cosmos db database if not exist
            var response = await cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseName);
            logger.LogInformation($"InitDBIfNotExistsAsync: CreateDatabaseIfNotExistsAsync status code:{response.StatusCode}");

            var database = cosmosClient.GetDatabase(DatabaseName);

            //create the user info collection if not exist
            var userInfoContainerResponse = database.CreateContainerIfNotExistsAsync(CollectionName, "/email");
            logger.LogInformation($"InitDBIfNotExistsAsync:  create {CollectionName} if not exist status code:{userInfoContainerResponse.Status}");

            _dbHasAlreadyInitiated = true;
        }

        private static async Task EnqueueResponseMessageAsync(DaprClient daprClient, string actionName, bool isSuccessful,
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
            await daprClient.InvokeBindingAsync("clientresponsequeue", "create", responseMessage);

        }
    }
}