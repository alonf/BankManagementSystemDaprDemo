using BMSD.Accessors.CheckingAccount.Contracts.Requests;
using BMSD.Accessors.CheckingAccount.Contracts.Responses;
using BMSD.Accessors.CheckingAccount.DB;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using System.Text.Json;

namespace BMSD.Accessors.CheckingAccount
{
    public class CheckingAccountAccessor
    {
        const string DatabaseName = "BMSDB";
        
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();
            builder.Services.AddControllers().AddDapr().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });
            
            using var loggerFactory = LoggerFactory.Create(loggingBuilder => loggingBuilder
                    .SetMinimumLevel(LogLevel.Trace)
                    .AddConsole());
            
            AddCosmosService(builder.Services, builder.Configuration, loggerFactory);
            
            var app = builder.Build();

            app.UseAuthorization();

            app.MapGet("/liveness", async (HttpContext httpContext) =>
            {
                await httpContext.Response.WriteAsync("OK");
            });

            //Update Account from queue
            app.MapPost("/accounttransactionqueue", async (HttpContext httpContext,
                    [FromServices] DaprClient daprClient, [FromServices] ILogger<CheckingAccountAccessor> logger,
                    [FromServices] ICosmosDBWrapper cosmosDBWrapper, [FromBody] AccountTransactionRequest requestItem) =>
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
                [FromServices] DaprClient daprClient, [FromServices] ILogger<CheckingAccountAccessor> logger,
                [FromServices] ICosmosDBWrapper cosmosDBWrapper, [FromQuery] string accountId) =>
            {
                logger.LogInformation("GetBalance HTTP trigger processed a request.");
                try
                {
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
                }
                catch (Exception ex)
                {
                    logger.LogError($"GetBalance: error: {ex}");
                    return Results.Problem("Error getting the balance");
                }
            });

            
            app.MapGet("/GetAccountInfo", async (HttpContext httpContext,
                DaprClient daprClient, [FromServices] ILogger<CheckingAccountAccessor> logger,
                [FromServices] ICosmosDBWrapper cosmosDBWrapper, [FromQuery] string accountId) =>
            {
                logger.LogInformation("GetAccountInfo HTTP trigger processed a request.");

                if (string.IsNullOrEmpty(accountId))
                {
                    logger.LogError("GetAccountInfo: missing account id parameter");
                    return Results.Problem("missing accountId parameter");
                }

                try
                {
                    var accountInfo = await cosmosDBWrapper.GetAccountInfoAsync(accountId);
                    return Results.Ok(accountInfo);
                }
                catch (Exception ex)
                {
                    logger.LogError($"GetAccountInfo: error: {ex}");
                    return Results.Problem("Error getting the account info");
                }
            });


            app.MapGet("/GetAccountTransactionHistory", async (HttpContext httpContext,
                DaprClient daprClient, [FromServices] ILogger<CheckingAccountAccessor> logger,
                [FromServices] ICosmosDBWrapper cosmosDBWrapper, [FromQuery] string accountId, [FromQuery] int? numberOfTransactions) =>
            {
                logger.LogInformation("GetAccountTransactionHistory HTTP trigger processed a request.");

                if (string.IsNullOrEmpty(accountId))
                {
                    logger.LogError("GetAccountTransactionHistory: missing account id parameter");
                    return Results.Problem("missing accountId parameter");
                }

                try
                {
                    numberOfTransactions ??= 10;
                    var transactions = await cosmosDBWrapper.GetAccountTransactionHistoryAsync(accountId, numberOfTransactions.Value);
                    return Results.Ok(transactions);
                }
                catch (Exception ex)
                {
                    logger.LogError($"GetAccountTransactionHistory: error: {ex}");
                    return Results.Problem("Error getting the account transaction history");
                }
            });

            app.Run();
        }

        public static void AddCosmosService(IServiceCollection services, IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            //get the cosmos db connection string from the configuration
            var cosmosDbConnectionString = configuration["CosmosDbConnectionString"];

            //Create Cosmos db client using cosmos client builder and camel case serializer
            //Important Security Note: To use CosmosDB emulator we ignore certification checks!!!
            var cosmosClient = new CosmosClientBuilder(cosmosDbConnectionString)
                .WithSerializerOptions(new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                })
                .WithHttpClientFactory(() =>
                {
                    HttpMessageHandler httpMessageHandler = new HttpClientHandler()
                    {
                        ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                    };
                    return new HttpClient(httpMessageHandler);
                })
                .WithConnectionModeGateway()
                .Build();

            //get logger from services
            var logger = loggerFactory.CreateLogger<CosmosDBWrapper>();

            var cosmosDBWrapper = new CosmosDBWrapper(cosmosClient, DatabaseName, logger);

            services.AddSingleton<ICosmosDBWrapper>(cosmosDBWrapper);
        }
    }
}