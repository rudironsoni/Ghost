using FluentAssertions;
using Ghost.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Extensions.Tests;

public class ServiceCollectionExtensionsTests : ReliabilityTestBase
{
    public ServiceCollectionExtensionsTests(ITestOutputHelper output) : base(output) { }

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
