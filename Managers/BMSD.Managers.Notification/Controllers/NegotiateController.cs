using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.SignalR.Management;
using Microsoft.Extensions.Configuration;

namespace BMSD.Managers.Notification.Controllers
{

    [ApiController]
    public class NegotiateController : ControllerBase
    {
        private const string EnableDetailedErrors = "EnableDetailedErrors";
        private readonly ServiceHubContext? _accountManagerCallbackHubContext;
        private readonly bool _enableDetailedErrors;

        public NegotiateController(IHubContextStore store, IConfiguration configuration)
        {
            _accountManagerCallbackHubContext = store.AccountManagerCallbackHubContext;
            _enableDetailedErrors = configuration.GetValue(EnableDetailedErrors, false);
        }

        [HttpPost("negotiate")]
        public Task<ActionResult> MessageHubNegotiate(string user)
        {
            return NegotiateBase(user, _accountManagerCallbackHubContext!);
        }

        
        private async Task<ActionResult> NegotiateBase(string user, ServiceHubContext serviceHubContext)
        {
            if (string.IsNullOrEmpty(user))
            {
                return BadRequest("User ID is null or empty.");
            }

            var negotiateResponse = await serviceHubContext.NegotiateAsync(new()
            {
                UserId = user,
                EnableDetailedErrors = _enableDetailedErrors
            });

            return new JsonResult(new Dictionary<string, string>()
            {
                { "url", negotiateResponse.Url! },
                { "accessToken", negotiateResponse.AccessToken! }
            });
        }
    }
}