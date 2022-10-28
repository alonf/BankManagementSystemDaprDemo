using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BMS.Engines.LiabilityValidator
{
    public class LiabilityValidatorEngine
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddAuthorization();

            builder.Services.AddControllers().AddDapr().AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            });

            var app = builder.Build();

            app.MapControllers();
            //app.UseAuthorization();



            app.MapGet("/CheckLiability", async (HttpContext httpContext, [FromServices] DaprClient daprClient,
                [FromServices] IConfiguration configuration, [FromServices] ILogger<LiabilityValidatorEngine> logger) =>
            {
                logger.LogInformation("CheckLiability: Checking Liability");
                try
                {
                    var accountId = httpContext.Request.Query["accountId"];

                    if (string.IsNullOrWhiteSpace(accountId))
                    {
                        return Results.Problem("Expecting the accountId parameter");
                    }

                    var amountText = httpContext.Request.Query["amount"];

                    if (string.IsNullOrWhiteSpace(amountText))
                    {
                        return Results.Problem("Expecting the amount parameter");
                    }

                    decimal amount = decimal.Parse(amountText);


                    var getBalanceResult = await daprClient.InvokeMethodAsync<JsonObject>(HttpMethod.Get, "checkingaccountaccessor", $"GetBalance?accountId={accountId}");

                    if (getBalanceResult == null)
                        return Results.Problem("Account not found");

                    var getAccountInfoResult = await daprClient.InvokeMethodAsync<JsonObject>(HttpMethod.Get, "checkingaccountaccessor", $"GetAccountInfo?accountId={accountId}");

                    if (getAccountInfoResult == null)
                        return Results.Problem("Account not found");

                    //get the balance as decimal value
                    var balance = getBalanceResult["balance"]!.AsValue().GetValue<decimal>();
                    var overdraftLimit = -(getAccountInfoResult["overdraftLimit"]!.AsValue().GetValue<decimal>());

                    var withdrawAllowed = balance - amount >= overdraftLimit;
                    logger.LogInformation($"Withdrawing {amount} from account id: {accountId} with balance of {balance} is" +
                                           (withdrawAllowed ? string.Empty : "not ") +
                                          $"allowed. The overdraft limit is: {overdraftLimit}");

                    return Results.Ok(JsonObject.Parse($"{{\"withdrawAllowed\":\"{withdrawAllowed}\"}}"));
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "CheckLiability: Error");
                    return Results.Problem("CheckLiability: Error");
                }
            });

            app.Run();
        }
    }
}