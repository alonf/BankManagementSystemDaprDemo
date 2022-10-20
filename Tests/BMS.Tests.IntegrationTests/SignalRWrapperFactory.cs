using Xunit.Abstractions;

namespace BMSD.Tests.IntegrationTests;

public class SignalRWrapperFactory : ISignalRWrapperFactory
{
    ISignalRWrapper ISignalRWrapperFactory.Create(ITestOutputHelper testOutputHelper)
    {
        return new SignalRWrapper(testOutputHelper);
    }
}