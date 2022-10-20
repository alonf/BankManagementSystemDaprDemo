using BMSD.Tests.IntegrationTests.Contracts;

namespace BMSD.Tests.IntegrationTests;
    
public interface ISignalRWrapper
{
    Task StartSignalR();
    Task<bool> WaitForSignalREventAsync(int timeoutInSeconds = 100);
    IList<AccountCallbackRequest> Messages { get; }
}