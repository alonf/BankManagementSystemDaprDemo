using BMSD.Tests.IntegrationTests.Contracts;
using BMSD.Tests.IntegrationTests.Logging;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace BMSD.Tests.IntegrationTests;

public class SignalRWrapper : ISignalRWrapper
{
    private readonly HubConnection _signalRHubConnection;
    private readonly List<AccountCallbackRequest> _signalRMessagesReceived = new ();
    private readonly SemaphoreSlim _signalRMessageReceived = new(0);

    public SignalRWrapper(ITestOutputHelper testOutputHelper)
    {
        var signalRUrl = Environment.GetEnvironmentVariable("BMS_SIGNALR_URL");
        if (string.IsNullOrEmpty(signalRUrl))
            signalRUrl = "http://localhost:7043/api/";

        _signalRHubConnection = new HubConnectionBuilder()
            .WithUrl(signalRUrl, c=>c.Headers.Add("x-application-user-id", "Teller1"))
            .WithAutomaticReconnect().ConfigureLogging(lb =>
            {
                lb.AddProvider(new XUnitLoggerProvider(testOutputHelper));
                lb.SetMinimumLevel(LogLevel.Debug);
            })
            .Build();
        
    }

    async Task ISignalRWrapper.StartSignalR()
    {
        _signalRMessagesReceived.Clear();

        if (_signalRHubConnection.State == HubConnectionState.Connected)
            return;

        await _signalRHubConnection.StartAsync();

        _signalRHubConnection.On<AccountCallbackRequest>("accountcallback", message =>
        {
            
            _signalRMessagesReceived.Add(message);
            _signalRMessageReceived.Release();
        });
    }

    async Task<bool> ISignalRWrapper.WaitForSignalREventAsync(int timeoutInSeconds)
    {
        var isSucceeded = await _signalRMessageReceived.WaitAsync(timeoutInSeconds * 1000);
        await Task.Delay(1000);
        return isSucceeded;
    }

    IList<AccountCallbackRequest> ISignalRWrapper.Messages => _signalRMessagesReceived;
}