using BMSD.Tests.IntegrationTests.Contracts;

namespace BMSD.Tests.IntegrationTests;
    
public interface ISignalRWrapper
{
    Task StartSignalR();
    Task<bool> WaitForSignalREventAsync(int timeoutInSeconds = 100);
    Task<bool> WaitForSignalREventWithConditionAsync(int timeoutInSeconds, Func<IList<AccountCallbackRequest>, bool> condition);
    IList<AccountCallbackRequest> Messages { get; }
}