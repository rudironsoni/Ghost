using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghostwright.Extensions.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGhostwriterKernel_RegistersOptionsAndLoggerFactory()
    {
        var services = new ServiceCollection();
        services.AddGhostwriterKernel();

        services.Should().Contain(sd => sd.ServiceType == typeof(Ghostwright.Core.KernelOptions));
        services.Should().Contain(sd => sd.ServiceType == typeof(Microsoft.Extensions.Logging.ILoggerFactory));
        // Do not resolve GhostwriterKernel here as it will attempt to launch a browser; ensure descriptor exists
        services.Should().Contain(sd => sd.ServiceType == typeof(Ghostwright.Core.GhostwriterKernel));
    }
}
