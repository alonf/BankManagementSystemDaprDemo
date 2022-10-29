using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using BMSD.Accessors.CheckingAccount.DB;
using BMSD.Accessors.CheckingAccount.Contracts.Responses;
using BMSD.Accessors.CheckingAccount.Contracts.Requests;
using Microsoft.Extensions.Logging;

namespace BMSD.Accessors.CheckingAccount.Controllers;

[ApiController]
[Route("[controller]")]
public class CheckingAccountController : ControllerBase
{
    private readonly ILogger<CheckingAccountController> _logger;
    private readonly DaprClient _daprClient;
    private readonly ICosmosDBWrapper _cosmosDBWrapper;


    public CheckingAccountController(ILogger<CheckingAccountController> logger, DaprClient daprClient,
            IConfiguration configuration, ICosmosDBWrapper cosmosDBWrapper)
    {
        _logger = logger;
        _daprClient = daprClient;
        _cosmosDBWrapper = cosmosDBWrapper;
    }

    [HttpPost("/accounttransactionqueue")]
    public async Task<IActionResult> HandleAccountTransactionQueueAsync([FromBody] AccountTransactionRequest requestItem)
    {
        if (string.IsNullOrWhiteSpace(requestItem.AccountId)
                        || string.IsNullOrWhiteSpace(requestItem.RequestId))
        {
            //this should never happen - if it occurs, it is a bug in the account manager validation
            _logger.LogError("UpdateAccount: Error: invalid account call: " +
                            (requestItem.AccountId ?? "is null") + " request Id:" +
                            (requestItem.RequestId ?? "is null"));
            return Problem("UpdateAccount: Error: invalid account call");
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
            _logger.LogInformation(
                $"UpdateAccount Queue trigger processed request id: {requestItem.RequestId}");
            await _cosmosDBWrapper.UpdateBalanceAsync(requestItem.RequestId, requestItem.AccountId,
                requestItem.Amount);
            await _daprClient.InvokeBindingAsync("clientresponsequeue", "create", responseCallBack);
        }
        catch (Exception ex)
        {
            _logger.LogError($"UpdateAccountAsync: error: {ex}");
            responseCallBack.IsSuccessful = false;
            responseCallBack.ResultMessage = "Error updating the balance. Retrying";

            await _daprClient.InvokeBindingAsync("clientresponsequeue", "create", responseCallBack);

            throw; //retry, todo: check if the error is transient to spare the retry
        }
        return Ok();
    }

    [HttpGet("/GetBalance")]
    public async Task<IActionResult> GetBalanceAsync([FromQuery] string accountId)
    {

        _logger.LogInformation("GetBalance HTTP trigger processed a request.");
        try
        {
            if (string.IsNullOrEmpty(accountId))
            {
                _logger.LogError("GetBalance: missing account id parameter");
                return Problem("missing accountId parameter");
            }

            var balance = await _cosmosDBWrapper.GetBalanceAsync(accountId);
            var balanceInfo = new BalanceInfo()
            {
                AccountId = accountId,
                Balance = balance
            };
            return Ok(balanceInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetBalance: error: {ex}");
            return Problem("Error getting the balance");
        }
    }

    [HttpGet("/GetAccountInfo")]
    public async Task<IActionResult> GetAccountInfoAsync([FromQuery] string accountId)
    {
        _logger.LogInformation("GetAccountInfo HTTP trigger processed a request.");

        if (string.IsNullOrEmpty(accountId))
        {
            _logger.LogError("GetAccountInfo: missing account id parameter");
            return Problem("missing accountId parameter");
        }

        try
        {
            var accountInfo = await _cosmosDBWrapper.GetAccountInfoAsync(accountId);
            return Ok(accountInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetAccountInfo: error: {ex}");
            return Problem("Error getting the account info");
        }
    }

    [HttpGet("/GetAccountTransactionHistory")]
    public async Task<IActionResult> GetAccountTransactionHistoryAsync([FromQuery] string accountId, [FromQuery] int numberOfTransactions = 10)
    {
        _logger.LogInformation("GetAccountTransactionHistory HTTP trigger processed a request.");

        if (string.IsNullOrEmpty(accountId))
        {
            _logger.LogError("GetAccountTransactionHistory: missing account id parameter");
            return Problem("missing accountId parameter");
        }

        try
        {
            var transactions = await _cosmosDBWrapper.GetAccountTransactionHistoryAsync(accountId, numberOfTransactions);
            return Ok(transactions);
        }
        catch (Exception ex)
        {
            _logger.LogError($"GetAccountTransactionHistory: error: {ex}");
            return Problem("Error getting the account transaction history");
        }
    }
}

