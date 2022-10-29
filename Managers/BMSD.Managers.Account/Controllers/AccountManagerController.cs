using AutoMapper;
using Microsoft.AspNetCore.Mvc;
using Dapr.Client;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BMSD.Managers.Account.Contracts.Responses;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BMSD.Managers.Account.Controllers;

[ApiController]
[Route("[controller]")]
public class AccountManagerController : ControllerBase
{
    private readonly ILogger<AccountManagerController> _logger;
    private readonly JsonSerializerOptions _serializerOptions;
    private readonly IMapper _mapper;
    private readonly DaprClient _daprClient;

    public AccountManagerController(ILogger<AccountManagerController> logger, DaprClient daprClient, IMapper mapper)
    {
        _logger = logger;
        _daprClient = daprClient;
        _mapper = mapper;
        _serializerOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };
    }

    [HttpGet("/Test")]
    public object GetTest()
    {
        var response = new
        {
            Message = "Hello from Account Manager"
        };
        return response;
    }

    [HttpPost("/RegisterCustomer")]
    public async Task<IActionResult> RegisterCustomer([FromBody] Contracts.Requests.CustomerRegistrationInfo customerRegistrationInfo)
    {
        _logger.LogInformation("HTTP trigger RegisterCustomer");
        try
        {
            //first check for idem-potency
            if (await RequestAlreadyProcessedAsync(customerRegistrationInfo.RequestId))
            {
                _logger.LogInformation($"RegisterCustomer request id {customerRegistrationInfo.RequestId} already processed");
                return Ok($"Request id {customerRegistrationInfo.RequestId} already processed");
            }
            //create a customer registration request for the User accessor
            var customerRegistrationInfoSubmit = _mapper.Map<Contracts.Submits.CustomerRegistrationInfo>(customerRegistrationInfo);
            var messagePayload = JsonSerializer.Serialize(customerRegistrationInfoSubmit, _serializerOptions);

            //push the customer registration request
            await _daprClient.InvokeBindingAsync("customerregistrationqueue", "create", customerRegistrationInfoSubmit);
            _logger.LogInformation($"RegisterCustomer request added: {messagePayload}");

            return Ok("Register customer request received");
        }
        catch (ValidationException validationException)
        {
            _logger.LogError($"RegisterCustomer: Input error, validation result: {validationException.Message}");
            return Problem(validationException.Message);
        }
        catch (Exception e)
        {
            _logger.LogError($"RegisterCustomer: Error occurred when processing a message: {e}");
            return Problem($"RegisterCustomer: Error occurred when processing a message: {e}");
        }
    }


    [HttpGet("/GetAccountId")]
    public async Task<IActionResult> GetAccountIdAsunc([FromQuery]string email)
    {
        _logger.LogInformation("HTTP trigger GetAccountId");
        try
        {
            var accountId =
                await _daprClient.InvokeMethodAsync<AccountIdInfo>(HttpMethod.Get, "userinfoaccessor", $"GetAccountIdByEmail?email={email}");

            if (accountId == null)
            {
                return Problem($"Account not found for email {email}");
            }
            return Ok(accountId);
        }
        catch (Exception e)
        {
            _logger.LogError($"GetAccountId: Error occurred when processing a message: {e}");
            return Problem($"GetAccountId: Error occurred when processing a message: {e}");
        }
    }


    [HttpGet("/GetAccountBalance")]
    public async Task<IActionResult> GetAccountBalanceAsync([FromQuery] string accountId)
    {
        _logger.LogInformation("HTTP trigger GetAccountBalance");
        try
        {
            var balanceInfo = await _daprClient.InvokeMethodAsync<Contracts.Responses.BalanceInfo>(HttpMethod.Get, "checkingaccountaccessor", $"GetBalance?accountId={accountId}");
            if (balanceInfo == null)
            {
                return Problem($"Account not found for id {accountId}");
            }
            return Ok(balanceInfo);
        }
        catch (Exception e)
        {
            _logger.LogError($"GetAccountBalance: Error occurred when processing a message: {e}");
            return Problem();
        }
    }


    [HttpGet("/GetAccountTransactionHistory")]
    public async Task<IActionResult> GetAccountTransactionHistoryAsync([FromQuery] string accountId, [FromQuery] int numberOfTransactions = 10)
    {
        _logger.LogInformation("HTTP trigger GetAccountTransactionHistory");
        try
        {
            var accountTransactionHistory = await _daprClient.InvokeMethodAsync<Contracts.Responses.AccountTransactionResponse[]>(
                    HttpMethod.Get, "checkingaccountaccessor", $"GetAccountTransactionHistory?accountId={accountId}&numberOfTransactions={numberOfTransactions}");

            if (accountTransactionHistory == null)
            {
                return Problem($"Account not found for id {accountId}");
            }
            return Ok(accountTransactionHistory);
        }
        catch (Exception e)
        {
            _logger.LogError($"GetAccountTransactionHistory: Error occurred when processing a message: {e}");
            return Problem($"GetAccountTransactionHistory: Error occurred when processing a message: {e}");
        }
    }


    [HttpPost("/Deposit")]
    public async Task<IActionResult> DepositAsync([FromBody] Contracts.Requests.AccountTransactionInfo accountTransactionInfo)
    {
        _logger.LogInformation("HTTP trigger Deposit");
        try
        {
            //first check for idem-potency
            if (await RequestAlreadyProcessedAsync(accountTransactionInfo.RequestId))
            {
                _logger.LogInformation($"Deposit request id {accountTransactionInfo.RequestId} already processed");
                return Ok($"Request id {accountTransactionInfo.RequestId} already processed");
            }
            //create a customer deposit request for the User accessor
            var accountTransactionSubmit = _mapper.Map<Contracts.Submits.AccountTransactionSubmit>(accountTransactionInfo);

            //push the deposit request
            await _daprClient.InvokeBindingAsync<Contracts.Submits.AccountTransactionSubmit>("accounttransactionqueue", "create", accountTransactionSubmit);
            _logger.LogInformation($"Deposit request added");

            return Ok("Deposit request received");
        }
        catch (Exception e)
        {
            _logger.LogError($"Deposit: Error occurred when processing a message: {e}");
            return Problem($"Deposit: Error occurred when processing a message: {e}");
        }
    }


    [HttpPost("/Withdraw")]
    public async Task<IActionResult> WithdrawAsync([FromBody] Contracts.Requests.AccountTransactionInfo accountTransactionInfo)
    {
        _logger.LogInformation("HTTP trigger Withdraw");
        try
        {
            //first check for idem-potency
            if (await RequestAlreadyProcessedAsync(accountTransactionInfo.RequestId))
            {
                _logger.LogInformation($"Withdraw request id {accountTransactionInfo.RequestId} already processed");
                return Ok($"Request id {accountTransactionInfo.RequestId} already processed");
            }

            //This is a naive solution, concurrent request may withdraw more monet than allowed
            if (!await CheckLiabilityAsync(accountTransactionInfo.AccountId!, accountTransactionInfo.Amount))
            {
                _logger.LogInformation("Withdraw request failed, the withdraw operation is forbidden");
                return BadRequest("The user is not allowed to withdraw");
            }

            accountTransactionInfo.Amount = -accountTransactionInfo.Amount;

            //create a customer withdraw request for the User accessor
            var accountTransactionSubmit = _mapper.Map<Contracts.Submits.AccountTransactionSubmit>(accountTransactionInfo);

            //push the deposit request
            await _daprClient.InvokeBindingAsync<Contracts.Submits.AccountTransactionSubmit>("accounttransactionqueue", "create", accountTransactionSubmit);
            _logger.LogInformation($"Deposit request added");

            return Ok("Withdraw request received");
        }
        catch (Exception e)
        {
            _logger.LogError($"Withdraw: Error occurred when processing a message: {e}");
            return Problem($"Withdraw: Error occurred when processing a message: {e}");
        }
    }

    private async Task<bool> RequestAlreadyProcessedAsync(string? requestId)
    {
        int retryCounter = 0;
        bool storeResult;
        do
        {
            if (++retryCounter > 10)
                throw new Exception("RequestAlreadyProcessedAsync: Failed to get the result from the store 10 times");

            var (result, etag) = await _daprClient.GetStateAndETagAsync<string>("processedrequests", requestId);
            if (result != null)
            {
                return true;
            }

            storeResult = await _daprClient.TrySaveStateAsync("processedrequests", requestId, "true", etag);
        } while (!storeResult);
        return false;
    }

    private async Task<bool> CheckLiabilityAsync(string accountId, decimal amount)
    {
        //Check liability
        var liabilityCheckResult = await _daprClient.InvokeMethodAsync<LiabilityResult>(
            HttpMethod.Get, "liabilityvalidatorengine", $"CheckLiability?accountId={accountId}&amount={amount.ToString(CultureInfo.InvariantCulture)}");

        if (liabilityCheckResult == null)
        {
            _logger.LogError($"CheckLiability: Liability check failed for account {accountId}");
            throw new Exception("liabilityValidator service returned an error");
        }

        return bool.Parse(liabilityCheckResult.WithdrawAllowed ?? "False");
    }
}
