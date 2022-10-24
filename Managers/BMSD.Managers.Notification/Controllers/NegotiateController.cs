using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
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

        [HttpPost("/negotiate")]
        public Task<ActionResult> MessageHubNegotiate(/*string user*/)
        {
            //get the user from the header
            var user = Request.Headers["x-application-user-id"];

            //throw if the user is not set
            if (string.IsNullOrEmpty(user))
                return Task.FromResult<ActionResult>(BadRequest("User is not set in the x-application-user-id header"));

            return NegotiateBase(user, _accountManagerCallbackHubContext!);
        }

        [HttpGet("/liveness")]
        public Task<ActionResult> Liveness()
        {
            return Task.FromResult<ActionResult>(Ok());
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