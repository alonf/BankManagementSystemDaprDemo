using Microsoft.Azure.SignalR.Management;

namespace BMSD.Managers.Notification;

public interface IHubContextStore
{
    public ServiceHubContext? AccountManagerCallbackHubContext { get; }
}