using FluentAssertions;
using Ghost.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Extensions.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGhostKernelServices_RegistersDeduplicationService()
    {
        var services = new ServiceCollection();
        services.AddGhostKernelServices();

        services.Should().Contain(sd => sd.ServiceType == typeof(IDeduplicationService));
        services.Should().Contain(sd => sd.ImplementationType == typeof(DeduplicationService));
        services.Should().Contain(sd => sd.Lifetime == ServiceLifetime.Singleton);
    }
}
