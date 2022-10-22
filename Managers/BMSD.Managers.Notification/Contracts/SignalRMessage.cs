using Microsoft.Azure.SignalR;
using Newtonsoft.Json;

namespace BMSD.Managers.Notification.Contracts
{
    public class SignalRMessage
    {
        public string? UserId { get; set; }
        public string? GroupName { get; set; }
        public string? Target { get; set; }
        //public Argument?[]? Arguments { get; set; }
        public Argument?[]? Arguments { get; set; }
    }
}
