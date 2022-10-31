using Microsoft.Azure.SignalR.Management;

namespace BMSD.Managers.Notification
{
    public class SignalRService : IHostedService, IHubContextStore
    {
        private const string AccountManagerCallbackHub = "accountmanagercallback";
        private readonly IConfiguration _configuration;
        private readonly ILoggerFactory _loggerFactory;

        public ServiceHubContext? AccountManagerCallbackHubContext { get; private set; }

        public SignalRService(IConfiguration configuration, ILoggerFactory loggerFactory)
        {
            _configuration = configuration;
            _loggerFactory = loggerFactory;
        }

        async Task IHostedService.StartAsync(CancellationToken cancellationToken)
        {
            using var serviceManager = new ServiceManagerBuilder()
                .WithConfiguration(_configuration)
                //or .WithOptions(o=>o.ConnectionString = _configuration["Azure:SignalR:ConnectionString"]
                .WithLoggerFactory(_loggerFactory)
                .BuildServiceManager();
            AccountManagerCallbackHubContext = await serviceManager.CreateHubContextAsync(AccountManagerCallbackHub, cancellationToken);
        }

        Task IHostedService.StopAsync(CancellationToken cancellationToken)
        {
            if (AccountManagerCallbackHubContext != null)
            {
                return AccountManagerCallbackHubContext.DisposeAsync();
            }
            return Task.CompletedTask;
        }

        // ReSharper disable once UnusedMember.Local
        private static Task Dispose(IServiceHubContext? hubContext)
        {
            return hubContext == null ? Task.CompletedTask : hubContext.DisposeAsync();
        }
    }
}