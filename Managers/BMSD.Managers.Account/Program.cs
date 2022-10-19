namespace BMSD.Managers.Account
{
    using AutoMapper;
    using BMSD.Managers.Account.Contracts.Responses;
    using Dapr.Client;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.WebUtilities;
    using System.ComponentModel.DataAnnotations;
    using System.Globalization;
    using System.Net.Http;
    using System.Reflection.Metadata.Ecma335;
    using System.Text.Json;
    using static Google.Rpc.Context.AttributeContext.Types;

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

            app.MapPost("/RegisterCustomer", async (HttpContext httpContext, ILogger logger, DaprClient daprClient, IMapper mapper) =>
            {
                logger.LogInformation("HTTP trigger RegisterCustomer");
                try
                {
                    // extract request from the body
                    string requestBody = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                    var data = JsonSerializer.Deserialize<Contracts.Requests.CustomerRegistrationInfo>(requestBody);

                    if (data == null)
                    {
                        return Results.Problem("Faild to read the request body");
                    }
                    Validator.ValidateObject(data, new ValidationContext(data, null, null));

                    //first check for idem-potency
                    if (await RequestAlreadyProcessedAsync(daprClient, data.RequestId))
                    {
                        logger.LogInformation($"RegisterCustomer request id {data.RequestId} already processed");
                        return Results.Problem($"Request id {data.RequestId} already processed");
                    }
                    //create a customer registration request for the User accessor
                    var customerRegistrationInfoSubmit = mapper.Map<Contracts.Submits.CustomerRegistrationInfo>(data);
                    var messagePayload = JsonSerializer.Serialize(customerRegistrationInfoSubmit);

                    //push the customer registration request
                    await daprClient.InvokeMethodAsync<Contracts.Submits.CustomerRegistrationInfo>("useraccessor", "RegisterCustomer", customerRegistrationInfoSubmit);
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
            });


            app.MapGet("/GetAccountId", async (HttpContext httpContext, ILogger logger, DaprClient daprClient, IMapper mapper) =>
            {
                logger.LogInformation("HTTP trigger GetAccountId");
                try
                {
                    var email = httpContext.Request.Query["email"];

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        return Results.Problem("Expecting the account owner email address");
                    }

                    var accountId = await daprClient.InvokeMethodAsync<string, AccountIdInfo>("useraccessor", "GetAccountId", $"email={email}");

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
            });


            app.MapGet("/GetAccountBalance", async (HttpContext httpContext, ILogger logger, DaprClient daprClient, IMapper mapper) =>
            {
                logger.LogInformation("HTTP trigger GetAccountBalance");
                try
                {
                    var accountId = httpContext.Request.Query["accountId"];

                    if (string.IsNullOrWhiteSpace(accountId))
                    {
                        return Results.Problem("Expecting the account id");
                    }

                    var balanceInfo = await daprClient.InvokeMethodAsync<string, Contracts.Responses.BalanceInfo>("checkingaccountaccessor", "GetAccountBalance", $"accountId={accountId}");

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
            });


            app.MapGet("/GetAccountTransactionHistory", async (HttpContext httpContext, ILogger logger, DaprClient daprClient, IMapper mapper) =>
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

                    var accountTransactionHistory = await daprClient.InvokeMethodAsync<string, Contracts.Responses.AccountTransactionResponse[]>(
                            "checkingaccountaccessor", "GetAccountTransactionHistory", $"accountId={accountId}&numberOfTransactions={numberOfTransactions}");

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
            });


            app.MapPost("/Deposit", async (HttpContext httpContext, ILogger logger, DaprClient daprClient, IMapper mapper) =>
            {
                logger.LogInformation("HTTP trigger Deposit");
                try
                {
                    // extract request from the body
                    string requestBody = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                    var data = JsonSerializer.Deserialize<Contracts.Requests.AccountTransactionInfo>(requestBody);

                    if (data == null)
                    {
                        return Results.Problem("Faild to read the request body");
                    }
                    Validator.ValidateObject(data, new ValidationContext(data, null, null));

                    //first check for idem-potency
                    if (await RequestAlreadyProcessedAsync(daprClient, data.RequestId))
                    {
                        logger.LogInformation($"Deposit request id {data.RequestId} already processed");
                        return Results.Problem($"Request id {data.RequestId} already processed");
                    }
                    //create a customer deposit request for the User accessor
                    var accountTransactionSubmit = mapper.Map<Contracts.Submits.AccountTransactionSubmit>(data);

                    //push the deposit request
                    await daprClient.InvokeMethodAsync<Contracts.Submits.AccountTransactionSubmit>("checkingaccountaccessor", "Deposit", accountTransactionSubmit);
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
            });


            app.MapPost("/Withdraw", async (HttpContext httpContext, ILogger logger, DaprClient daprClient, IMapper mapper) =>
            {
                logger.LogInformation("HTTP trigger Withdraw");
                try
                {
                    // extract request from the body
                    string requestBody = await new StreamReader(httpContext.Request.Body).ReadToEndAsync();
                    var data = JsonSerializer.Deserialize<Contracts.Requests.AccountTransactionInfo>(requestBody);
                    
                    if (data == null || data.AccountId == null)
                    {
                        return Results.Problem("Faild to read the request body");
                    }
                    Validator.ValidateObject(data, new ValidationContext(data, null, null));
                    
                    //first check for idem-potency
                    if (await RequestAlreadyProcessedAsync(daprClient, data.RequestId))
                    {
                        logger.LogInformation($"Withdraw request id {data.RequestId} already processed");
                        return Results.Problem($"Request id {data.RequestId} already processed");
                    }
                    
                    //This is a naive solution, concurrent request may withdraw more monet than allowed
                    if (!await CheckLiabilityAsync(daprClient, logger, data.AccountId, data.Amount))
                    {
                        logger.LogInformation("Withdraw request failed, the withdraw operation is forbidden");
                        return Results.Problem("The user is not allowed to withdraw");
                    }
                    
                    data.Amount = -data.Amount;

                    //create a customer withdraw request for the User accessor
                    var accountTransactionSubmit = mapper.Map<Contracts.Submits.AccountTransactionSubmit>(data);

                    //push the deposit request
                    await daprClient.InvokeMethodAsync<Contracts.Submits.AccountTransactionSubmit>("checkingaccountaccessor", "Withdraw", accountTransactionSubmit);
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
            });


            app.Run();
        }

        private static async Task<bool> RequestAlreadyProcessedAsync(DaprClient daprClient, string? requestId)
        {
            var result = await daprClient.GetStateAsync<string>("processedrequests", requestId);
            if (result != null)
            {
                return true;
            }

            await daprClient.SaveStateAsync("processedrequests", requestId, "true");
            return false; 
        }

        private static async Task<bool> CheckLiabilityAsync(DaprClient daprClient, ILogger logger, string accountId, decimal amount)
        {
            //Check liability
            var liabilityCheckResult = await daprClient.InvokeMethodAsync<string, LiabilityResult>(
                "liabilityaccessor", "CheckLiability", $"accountId={accountId}&amount={amount.ToString(CultureInfo.InvariantCulture)}");


            if (liabilityCheckResult == null)
            {
                logger.LogError($"CheckLiability: Liability check failed for account {accountId}");
                throw new Exception("liabilityValidator service returned an error");
            }

            return liabilityCheckResult.WithdrawAllowed;
        }
    }
}