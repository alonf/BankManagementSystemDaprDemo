using Xunit.Abstractions;

namespace BMSD.Tests.IntegrationTests;

public interface ISignalRWrapperFactory
{
    ISignalRWrapper Create(ITestOutputHelper testOutputHelper);
}