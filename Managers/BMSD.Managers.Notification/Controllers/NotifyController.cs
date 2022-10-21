using BMSD.Managers.Notification.Contracts;
using Dapr.Client;
using Microsoft.AspNetCore.Mvc;

namespace BMSD.Managers.Notification.Controllers
{
    public class NotifyController : Controller
    {
        private readonly DaprClient _daprClient;
        private readonly ILogger<NotifyController> _logger;

        public NotifyController(DaprClient daprClient, ILogger<NotifyController> logger)
        {
            _daprClient = daprClient;
            _logger = logger;
        }

        [HttpPost("/clientresponsequeue")]
        public async Task<IActionResult> AccountCallbackHandlerAsync([FromBody] Contracts.AccountCallbackRequest accountCallbackRequest)
        {
            try
            {
                _logger.LogInformation($"Received response: {accountCallbackRequest}");
                return await PublishMessageToSignalRAsync(accountCallbackRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError($"AccountCallbackHandlerAsync: Error: {ex.Message}");
                if (ex.InnerException != null)
                    _logger.LogError($"AccountCallbackHandlerAsync: inner exception: {ex.InnerException.Message}");
            }
            return Problem(statusCode: StatusCodes.Status500InternalServerError);
        }
        
        private async Task<IActionResult> PublishMessageToSignalRAsync(AccountCallbackRequest accountCallbackRequest)
        {
            Argument argument = new Argument
            {
                Sender = "dapr",
                Text = accountCallbackRequest
            };

            SignalRMessage message = new()
            {
                UserId = accountCallbackRequest.CallerId,
                Target = "accountCallback",
                Arguments = new[] { argument }
            };

            await _daprClient.InvokeBindingAsync("clientcallback", "create", message);
            return Ok();
        }
    }
}
