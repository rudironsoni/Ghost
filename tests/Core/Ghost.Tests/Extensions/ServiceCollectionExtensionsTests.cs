using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Extensions.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGhostKernel_RegistersOptionsAndLoggerFactory()
    {
        var services = new ServiceCollection();
        services.AddGhostKernel();

        services.Should().Contain(sd => sd.ServiceType == typeof(Ghost.Core.KernelOptions));
        services.Should().Contain(sd => sd.ServiceType == typeof(Microsoft.Extensions.Logging.ILoggerFactory));
        // Do not resolve GhostKernel here as it will attempt to launch a browser; ensure descriptor exists
        services.Should().Contain(sd => sd.ServiceType == typeof(Ghost.Core.GhostKernel));
    }
}
