using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Nodes;
using Dapr.Client;

namespace BMSD.Engines.LiabilityValidator.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class LiabilityValidatorController : ControllerBase
    {
        private readonly ILogger<LiabilityValidatorController> _logger;
        private readonly DaprClient _daprClient;

        public LiabilityValidatorController(ILogger<LiabilityValidatorController> logger, DaprClient daprClient)
        {
            _logger = logger;
            _daprClient = daprClient;
        }

        [HttpGet("/CheckLiability")]
        public async Task<IActionResult> CheckLiabilityAsync([FromQuery] string accountId, [FromQuery] decimal amount)
        {
            _logger.LogInformation("CheckLiability: Checking Liability");
            try
            {
                var getBalanceResult = await _daprClient.InvokeMethodAsync<JsonObject>(HttpMethod.Get, "checkingaccountaccessor", $"GetBalance?accountId={accountId}");

                if (getBalanceResult == null)
                    return Problem("Account not found");

                var getAccountInfoResult = await _daprClient.InvokeMethodAsync<JsonObject>(HttpMethod.Get, "checkingaccountaccessor", $"GetAccountInfo?accountId={accountId}");

                if (getAccountInfoResult == null)
                    return Problem("Account not found");

                //get the balance as decimal value
                var balance = getBalanceResult["balance"]!.AsValue().GetValue<decimal>();
                var overdraftLimit = -(getAccountInfoResult["overdraftLimit"]!.AsValue().GetValue<decimal>());

                var withdrawAllowed = balance - amount >= overdraftLimit;
                _logger.LogInformation($"Withdrawing {amount} from account id: {accountId} with balance of {balance} is" +
                                       (withdrawAllowed ? string.Empty : "not ") +
                                      $"allowed. The overdraft limit is: {overdraftLimit}");

                return Ok(JsonObject.Parse($"{{\"withdrawAllowed\":\"{withdrawAllowed}\"}}"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CheckLiability: Error");
                return Problem("CheckLiability: Error");
            }
        }
    }
}