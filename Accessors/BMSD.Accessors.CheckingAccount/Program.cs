using BMSD.Accessors.CheckingAccount.Contracts.Requests;
using BMSD.Accessors.CheckingAccount.Contracts.Responses;
using BMSD.Accessors.CheckingAccount.DB;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;

namespace BMSD.Accessors.CheckingAccount
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            app.UseHttpsRedirection();

            app.UseAuthorization();

            builder.Services.AddControllers().AddDapr();

            //Update Account from queue
            app.MapPost("/accounttransactionqueue", async (HttpContext httpContext,
                    DaprClient daprClient, ILogger logger,
                    ICosmosDBWrapper cosmosDBWrapper, [FromBody] AccountTransactionRequest requestItem) =>
                {
                    if (string.IsNullOrWhiteSpace(requestItem.AccountId)
                        || string.IsNullOrWhiteSpace(requestItem.RequestId))
                    {
                        //this should never happen - if it occurs, it is a bug in the account manager validation
                        logger.LogError("UpdateAccount: Error: invalid account call: " +
                                        (requestItem.AccountId ?? "is null") + " request Id:" +
                                        (requestItem.RequestId ?? "is null"));
                        return Results.Problem("UpdateAccount: Error: invalid account call");
                    }

                    var responseCallBack = new AccountCallbackResponse()
                    {
                        AccountId = requestItem.AccountId,
                        RequestId = requestItem.RequestId,
                        ActionName = requestItem.Amount > 0 ? "Deposit" : "Withdraw",
                        IsSuccessful = true,
                        ResultMessage = "Account Balance Updated"
                    };

                    try
                    {
                        logger.LogInformation(
                            $"UpdateAccount Queue trigger processed request id: {requestItem.RequestId}");
                        await cosmosDBWrapper.UpdateBalanceAsync(requestItem.RequestId, requestItem.AccountId,
                            requestItem.Amount);
                        await daprClient.InvokeBindingAsync("clientresponsequeue", "create", responseCallBack);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError($"UpdateAccountAsync: error: {ex}");
                        responseCallBack.IsSuccessful = false;
                        responseCallBack.ResultMessage = "Error updating the balance. Retrying";

                        await daprClient.InvokeBindingAsync("clientresponsequeue", "create", responseCallBack);

                        throw; //retry, todo: check if the error is transient to spare the retry
                    }
                    return Results.Ok();
                });

            app.MapGet("/GetBalance", async (HttpContext httpContext,
                DaprClient daprClient, ILogger logger,
                ICosmosDBWrapper cosmosDBWrapper, [FromQuery] string accountId) =>
            {
                logger.LogInformation("GetBalance HTTP trigger processed a request.");

                if (string.IsNullOrEmpty(accountId))
                {
                    logger.LogError("GetBalance: missing account id parameter");
                    return Results.Problem("missing accountId parameter");
                }

                var balance = await cosmosDBWrapper.GetBalanceAsync(accountId);
                var balanceInfo = new BalanceInfo()
                {
                    AccountId = accountId,
                    Balance = balance
                };
                return Results.Ok(balanceInfo);
            });

            
            app.MapGet("/GetAccountInfo", async (HttpContext httpContext,
                DaprClient daprClient, ILogger logger,
                ICosmosDBWrapper cosmosDBWrapper, [FromQuery] string accountId) =>
            {
                logger.LogInformation("GetAccountInfo HTTP trigger processed a request.");

                if (string.IsNullOrEmpty(accountId))
                {
                    logger.LogError("GetAccountInfo: missing account id parameter");
                    return Results.Problem("missing accountId parameter");
                }

                var accountInfo = await cosmosDBWrapper.GetAccountInfoAsync(accountId);
                return Results.Ok(accountInfo);
            });


            app.MapGet("/GetAccountTransactionHistory", async (HttpContext httpContext,
                DaprClient daprClient, ILogger logger,
                ICosmosDBWrapper cosmosDBWrapper, [FromQuery] string accountId, [FromQuery] int? numberOfTransactions) =>
            {
                logger.LogInformation("GetAccountTransactionHistory HTTP trigger processed a request.");

                if (string.IsNullOrEmpty(accountId))
                {
                    logger.LogError("GetAccountTransactionHistory: missing account id parameter");
                    return Results.Problem("missing accountId parameter");
                }

                numberOfTransactions ??= 10;

                var transactions = await cosmosDBWrapper.GetAccountTransactionHistoryAsync(accountId, numberOfTransactions.Value);

                return Results.Ok(transactions);
            });

            app.Run();
        }

        public void ConfigureServices(IServiceCollection services, IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            //get the cosmos db connection string from the configuration
            var cosmosDbConnectionString = configuration["CosmosDbConnectionString"];

            //get cosmos db checking account database name from configuration
            var cosmosDbCheckingAccountDatabaseName = configuration["CosmosDbCheckingAccountDatabaseName"];

            //Create Cosmos db client using cosmos client builder and camel case serializer
            var cosmosClient = new CosmosClientBuilder(cosmosDbConnectionString)
                .WithSerializerOptions(new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                })
                .Build();

            //get logger from services
            var logger = loggerFactory.CreateLogger<CosmosDBWrapper>();

            var cosmosDBWrapper = new CosmosDBWrapper(cosmosClient, cosmosDbCheckingAccountDatabaseName, logger);

            services.AddSingleton<ICosmosDBWrapper>(cosmosDBWrapper);
        }
    }
}