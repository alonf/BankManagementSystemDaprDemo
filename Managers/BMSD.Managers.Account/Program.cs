namespace BMSD.Managers.Account
{
    using AutoMapper;
    using Contracts.Responses;
    using Dapr.Client;
    using Microsoft.AspNetCore.Mvc;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.Text.Json;

    public class AccountManager
    {
        public static void Main(string[] args)
        {
            Console.WriteLine("*** Account Manager is starting ***");

            try
            {
                var builder = WebApplication.CreateBuilder(args);
                //var MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
                //// Add services to the container.
                //builder.Services.AddCors(options =>
                //{
                //    options.AddPolicy(name: MyAllowSpecificOrigins,
                //        policy =>
                //        {
                //            policy.AllowAnyOrigin().WithMethods("PUT", "POST", "DELETE", "GET");
                //        });
                //});
                
                
                builder.Services.AddHealthChecks();

                // Add services to the container.
                builder.Services.AddAuthorization();
                builder.Services.AddControllers().AddDapr().AddJsonOptions(options =>
                {
                    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                });

                try
                {
                    var mapperConfig = new MapperConfiguration(mc => { mc.AddProfile(new AutoMappingProfile()); });
                    IMapper mapper = mapperConfig.CreateMapper();
                    mapperConfig.AssertConfigurationIsValid();
                    builder.Services.AddSingleton(mapper);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    throw;
                }

                var app = builder.Build();

                //app.UseCors(MyAllowSpecificOrigins);

                //app.UseHttpsRedirection();

                //app.Urls.Add("http://*:80");
                
                //app.MapControllers();
                
                app.MapHealthChecks("/healthz");

                app.UseAuthorization();


                var serializeOptions = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                //endpoint for simple test
                app.MapGet("/Test", async (HttpContext httpContext) =>
                {
                    var response = new
                    {
                        Message = "Hello from Account Manager"
                    };
                    await httpContext.Response.WriteAsJsonAsync(response, serializeOptions);
                });


                app.MapPost("/RegisterCustomer", async (HttpContext httpContext, [FromServices] ILogger<AccountManager> logger, [FromServices] DaprClient daprClient, [FromServices] IMapper mapper) =>
                {
                    logger.LogInformation("HTTP trigger RegisterCustomer");
                    try
                    {
                        // extract request from the body
                        string requestBody = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                        var data = JsonSerializer.Deserialize<Contracts.Requests.CustomerRegistrationInfo>(requestBody, serializeOptions);

                        if (data == null)
                        {
                            return Results.Problem("Fail to read the request body");
                        }
                        Validator.ValidateObject(data, new ValidationContext(data, null, null));

                        //first check for idem-potency
                        if (await RequestAlreadyProcessedAsync(daprClient, data.RequestId))
                        {
                            logger.LogInformation($"RegisterCustomer request id {data.RequestId} already processed");
                            return Results.Ok($"Request id {data.RequestId} already processed");
                        }
                        //create a customer registration request for the User accessor
                        var customerRegistrationInfoSubmit = mapper.Map<Contracts.Submits.CustomerRegistrationInfo>(data);
                        var messagePayload = JsonSerializer.Serialize(customerRegistrationInfoSubmit, serializeOptions);

                        //push the customer registration request
                        await daprClient.InvokeBindingAsync("customerregistrationqueue", "create", customerRegistrationInfoSubmit);
                        logger.LogInformation($"RegisterCustomer request added: {messagePayload}");

                        return Results.Ok("Register customer request received");
                    }
                    catch (ValidationException validationException)
                    {
                        logger.LogError($"RegisterCustomer: Input error, validation result: {validationException.Message}");
                        return Results.Problem(validationException.Message);
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"RegisterCustomer: Error occurred when processing a message: {e}");
                        return Results.Problem();
                    }
                }).WithName("RegisterCustomer");


                app.MapGet("/GetAccountId",
                    async (HttpContext httpContext, [FromServices] ILogger<AccountManager> logger, [FromServices] DaprClient daprClient, [FromServices] IMapper mapper) =>
                {
                    logger.LogInformation("HTTP trigger GetAccountId");
                    try
                    {
                        var email = httpContext.Request.Query["email"];

                        if (string.IsNullOrWhiteSpace(email))
                        {
                            return Results.Problem("Expecting the account owner email address");
                        }

                        var accountId =
                            await daprClient.InvokeMethodAsync<AccountIdInfo>(HttpMethod.Get, "userinfoaccessor", $"GetAccountIdByEmail?email={email}");

                        if (accountId == null)
                        {
                            return Results.Problem($"Account not found for email {email}");
                        }
                        return Results.Ok(accountId);
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"GetAccountId: Error occurred when processing a message: {e}");
                        return Results.Problem();
                    }
                }).WithName("GetAccountId");


                app.MapGet("/GetAccountBalance", async (HttpContext httpContext, [FromServices] ILogger<AccountManager> logger,
                    [FromServices] DaprClient daprClient, [FromServices] IMapper mapper) =>
                {
                    logger.LogInformation("HTTP trigger GetAccountBalance");
                    try
                    {
                        var accountId = httpContext.Request.Query["accountId"];

                        if (string.IsNullOrWhiteSpace(accountId))
                        {
                            return Results.Problem("Expecting the account id");
                        }

                        var balanceInfo = await daprClient.InvokeMethodAsync<Contracts.Responses.BalanceInfo>(HttpMethod.Get, "checkingaccountaccessor", $"GetBalance?accountId={accountId}");
                        if (balanceInfo == null)
                        {
                            return Results.Problem($"Account not found for id {accountId}");
                        }
                        return Results.Ok(balanceInfo);
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"GetAccountBalance: Error occurred when processing a message: {e}");
                        return Results.Problem();
                    }
                }).WithName("GetAccountBalance");


                app.MapGet("/GetAccountTransactionHistory", async (HttpContext httpContext,
                    [FromServices] ILogger<AccountManager> logger, [FromServices] DaprClient daprClient, [FromServices] IMapper mapper) =>
                {
                    logger.LogInformation("HTTP trigger GetAccountTransactionHistory");
                    try
                    {
                        var accountId = httpContext.Request.Query["accountId"];

                        if (string.IsNullOrWhiteSpace(accountId))
                        {
                            return Results.Problem("Expecting the account id");
                        }

                        string numberOfTransactions = httpContext.Request.Query["numberOfTransactions"];

                        if (string.IsNullOrEmpty(numberOfTransactions))
                        {
                            numberOfTransactions = "10"; //default
                        }

                        var accountTransactionHistory = await daprClient.InvokeMethodAsync<Contracts.Responses.AccountTransactionResponse[]>(
                                HttpMethod.Get, "checkingaccountaccessor", $"GetAccountTransactionHistory?accountId={accountId}&numberOfTransactions={numberOfTransactions}");

                        if (accountTransactionHistory == null)
                        {
                            return Results.Problem($"Account not found for id {accountId}");
                        }
                        return Results.Ok(accountTransactionHistory);
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"GetAccountTransactionHistory: Error occurred when processing a message: {e}");
                        return Results.Problem();
                    }
                }).WithName("GetAccountTransactionHistory");


                app.MapPost("/Deposit", async (HttpContext httpContext,
                    [FromServices] ILogger<AccountManager> logger, [FromServices] DaprClient daprClient, [FromServices] IMapper mapper) =>
                {
                    logger.LogInformation("HTTP trigger Deposit");
                    try
                    {
                        // extract request from the body
                        string requestBody = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                        var data = JsonSerializer.Deserialize<Contracts.Requests.AccountTransactionInfo>(requestBody, serializeOptions);

                        if (data == null)
                        {
                            return Results.Problem("Faild to read the request body");
                        }
                        Validator.ValidateObject(data, new ValidationContext(data, null, null));

                        //first check for idem-potency
                        if (await RequestAlreadyProcessedAsync(daprClient, data.RequestId))
                        {
                            logger.LogInformation($"Deposit request id {data.RequestId} already processed");
                            return Results.Ok($"Request id {data.RequestId} already processed");
                        }
                        //create a customer deposit request for the User accessor
                        var accountTransactionSubmit = mapper.Map<Contracts.Submits.AccountTransactionSubmit>(data);

                        //push the deposit request
                        await daprClient.InvokeBindingAsync<Contracts.Submits.AccountTransactionSubmit>("accounttransactionqueue", "create", accountTransactionSubmit);
                        logger.LogInformation($"Deposit request added");

                        return Results.Ok("Deposit request received");
                    }
                    catch (ValidationException validationException)
                    {
                        logger.LogError($"Deposit: Input error, validation result: {validationException.Message}");
                        return Results.Problem(validationException.Message);
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Deposit: Error occurred when processing a message: {e}");
                        return Results.Problem();
                    }
                }).WithName("Deposit");


                app.MapPost("/Withdraw", async (HttpContext httpContext,
                    [FromServices] ILogger<AccountManager> logger, [FromServices] DaprClient daprClient, [FromServices] IMapper mapper) =>
                {
                    logger.LogInformation("HTTP trigger Withdraw");
                    try
                    {
                        // extract request from the body
                        string requestBody = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                        var data = JsonSerializer.Deserialize<Contracts.Requests.AccountTransactionInfo>(requestBody, serializeOptions);

                        if (data == null || data.AccountId == null)
                        {
                            return Results.Problem("Faild to read the request body");
                        }
                        Validator.ValidateObject(data, new ValidationContext(data, null, null));

                        //first check for idem-potency
                        if (await RequestAlreadyProcessedAsync(daprClient, data.RequestId))
                        {
                            logger.LogInformation($"Withdraw request id {data.RequestId} already processed");
                            return Results.Ok($"Request id {data.RequestId} already processed");
                        }

                        //This is a naive solution, concurrent request may withdraw more monet than allowed
                        if (!await CheckLiabilityAsync(daprClient, logger, data.AccountId, data.Amount))
                        {
                            logger.LogInformation("Withdraw request failed, the withdraw operation is forbidden");
                            return Results.BadRequest("The user is not allowed to withdraw");
                        }

                        data.Amount = -data.Amount;

                        //create a customer withdraw request for the User accessor
                        var accountTransactionSubmit = mapper.Map<Contracts.Submits.AccountTransactionSubmit>(data);

                        //push the deposit request
                        await daprClient.InvokeBindingAsync<Contracts.Submits.AccountTransactionSubmit>("accounttransactionqueue", "create", accountTransactionSubmit);
                        logger.LogInformation($"Deposit request added");

                        return Results.Ok("Withdraw request received");
                    }
                    catch (ValidationException validationException)
                    {
                        logger.LogError($"Withdraw: Input error, validation result: {validationException.Message}");
                        return Results.Problem(validationException.Message);
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Withdraw: Error occurred when processing a message: {e}");
                        return Results.Problem();
                    }
                }).WithName("Withdraw"); ;


                app.Run();
            }
            catch (Exception e)
            {
                Console.WriteLine($"*** Application Failure: exception: {e} ***");
                throw;
            }
        }
            

        private static async Task<bool> RequestAlreadyProcessedAsync(DaprClient daprClient, string? requestId)
        {
            int retryCounter = 0;
            bool storeResult;
            do
            {
                if (++retryCounter > 10)
                    throw new Exception("RequestAlreadyProcessedAsync: Failed to get the result from the store 10 times");
                
                var (result, etag) = await daprClient.GetStateAndETagAsync<string>("processedrequests", requestId);
                if (result != null)
                {
                    return true;
                }

                storeResult = await daprClient.TrySaveStateAsync("processedrequests", requestId, "true", etag);
            } while (!storeResult);
            return false;
        }

        private static async Task<bool> CheckLiabilityAsync(DaprClient daprClient, ILogger logger, string accountId, decimal amount)
        {
            //Check liability
            var liabilityCheckResult = await daprClient.InvokeMethodAsync<LiabilityResult>(
                HttpMethod.Get, "liabilityvalidatorengine", $"CheckLiability?accountId={accountId}&amount={amount.ToString(CultureInfo.InvariantCulture)}");

            if (liabilityCheckResult == null)
            {
                logger.LogError($"CheckLiability: Liability check failed for account {accountId}");
                throw new Exception("liabilityValidator service returned an error");
            }

            return bool.Parse(liabilityCheckResult.WithdrawAllowed ?? "False");
        }
    }
}