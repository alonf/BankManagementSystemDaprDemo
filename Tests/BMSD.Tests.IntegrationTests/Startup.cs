using Microsoft.Extensions.DependencyInjection;

namespace BMSD.Tests.IntegrationTests;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        var accountManagerUrl = Environment.GetEnvironmentVariable("ACCOUNT_MANAGER_URL");
        if (string.IsNullOrEmpty(accountManagerUrl))
            accountManagerUrl = "http://localhost:3500/v1.0/invoke/accountmanager/method/";

        services.AddRobustHttpClient<IntegrationTest>(baseUrl: accountManagerUrl);
        services.AddSingleton<ISignalRWrapperFactory, SignalRWrapperFactory>();
    }
    
}
