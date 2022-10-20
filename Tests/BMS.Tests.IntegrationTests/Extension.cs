using Microsoft.Extensions.DependencyInjection;
using Polly.Extensions.Http;
using Polly;

namespace BMSD.Tests.IntegrationTests;

public static class Extension
{
    private static readonly Random _jitterer = new Random();
    public static IHttpClientBuilder AddRobustHttpClient<TClient, TImplementation>(
        this IServiceCollection services, int retryCount = 5,
        int handledEventsAllowedBeforeBreaking = 5, int durationOfBreakInSeconds = 30, string baseUrl = null)
        where TClient : class where TImplementation : class, TClient
    {
        var httpClientBuilder = baseUrl != null ?
            services.AddHttpClient<TClient, TImplementation>(typeof(TClient).Name, c => c.BaseAddress = new Uri(baseUrl)) :
            services.AddHttpClient<TClient, TImplementation>();

        return httpClientBuilder
            .AddPolicyHandler(GetRetryPolicy(retryCount))
            .AddPolicyHandler(GetCircuitBreakerPolicy(handledEventsAllowedBeforeBreaking, durationOfBreakInSeconds));
    }

    public static IHttpClientBuilder AddRobustHttpClient<TClient>(
        this IServiceCollection services, int retryCount = 5,
        int handledEventsAllowedBeforeBreaking = 5, int durationOfBreakInSeconds = 30, string baseUrl = null)
        where TClient : class
    {
        var httpClientBuilder = baseUrl != null ?
            services.AddHttpClient<TClient>(typeof(TClient).Name, c => c.BaseAddress = new Uri(baseUrl)) :
            services.AddHttpClient<TClient>();

        return httpClientBuilder.AddPolicyHandler(GetRetryPolicy(retryCount))
            .AddPolicyHandler(GetCircuitBreakerPolicy(handledEventsAllowedBeforeBreaking, durationOfBreakInSeconds));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy(int retryCount)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(retryCount, // exponential back-off plus some jitter
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                                + TimeSpan.FromMilliseconds(_jitterer.Next(0, 100)));
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy(
        int handledEventsAllowedBeforeBreaking, int durationOfBreakInSeconds)
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(handledEventsAllowedBeforeBreaking, TimeSpan.FromSeconds(durationOfBreakInSeconds));
    }
}