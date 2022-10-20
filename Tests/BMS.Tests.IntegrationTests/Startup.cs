using Microsoft.Extensions.DependencyInjection;

namespace BMSD.Tests.IntegrationTests;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        var accountManagerUrl = Environment.GetEnvironmentVariable("ACCOUNT_MANAGER_URL");
        if (string.IsNullOrEmpty(accountManagerUrl))
            accountManagerUrl = "http://localhost:7071/api/";

        services.AddRobustHttpClient<IntegrationTest>(baseUrl: accountManagerUrl);
        services.AddSingleton<ISignalRWrapperFactory, SignalRWrapperFactory>();
    }
    
}
