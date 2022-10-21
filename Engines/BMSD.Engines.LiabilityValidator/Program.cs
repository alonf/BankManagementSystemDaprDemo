using Dapr.Client;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;

namespace BMS.Engines.LiabilityValidator
{
    public class Program
    {
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


            app.MapGet("/CheckLiability", async (HttpContext httpContext, DaprClient daprClient, 
                IConfiguration configuration, ILogger logger) =>
            {
                logger.LogInformation("CheckLiability: Checking Liability");
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


                var getBalanceResult = await daprClient.InvokeMethodAsync<string,JsonObject>("accountinfoaccessor", "GetBalance", $"accountId = {accountId}");

                if (getBalanceResult == null)
                    return Results.Problem("Account not found");


                var getOverdraftLimitResult = await daprClient.InvokeMethodAsync<string, JsonObject>("accountinfoaccessor", "overdraftLimit", $"accountId = {accountId}");

                if (getOverdraftLimitResult == null)
                    return Results.Problem("Account not found");

                //get the balance as decimal value
                var balance = getBalanceResult["balance"]!.AsValue().GetValue<decimal>();
                var overdraftLimit = -(getBalanceResult["overdraftLimit"]!.AsValue().GetValue<decimal>());

                var withdrawAllowed = balance - amount >= overdraftLimit;
                logger.LogInformation($"Withdrawing {amount} from account id: {accountId} with balance of {balance} is" +
                                       (withdrawAllowed ? string.Empty : "not ") +
                                      $"allowed. The overdraft limit is: {overdraftLimit}");

                return Results.Ok(JsonObject.Parse($"{{'withdrawAllowed':'{withdrawAllowed}'}}"));
            });

            app.Run();
        }
    }
}