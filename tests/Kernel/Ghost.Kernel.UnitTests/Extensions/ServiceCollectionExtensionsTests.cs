using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Extensions.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    [Obsolete("This test validates the obsolete AddGhostKernel method. Use GhostKernelManager instead.")]
    public void AddGhostKernelRegistersOptionsAndLoggerFactory()
    {
        var services = new ServiceCollection();
        services.AddGhostKernel();

        services.Should().Contain(sd => sd.ServiceType == typeof(Ghost.Kernel.KernelOptions));
        services.Should().Contain(sd => sd.ServiceType == typeof(Microsoft.Extensions.Logging.ILoggerFactory));
        // Do not resolve GhostKernel here as it will attempt to launch a browser; ensure descriptor exists
        services.Should().Contain(sd => sd.ServiceType == typeof(Ghost.Kernel.GhostKernel));
    }
}
