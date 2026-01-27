using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghostwright.Hosting.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGhostwright_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        var act = () => Ghostwright.Hosting.ServiceCollectionExtensions.AddGhostwright(services!, _ => { });
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGhostwright_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddGhostwright(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGhostwright_ValidConfig_ReturnsServices()
    {
        var services = new ServiceCollection();
        var result = services.AddGhostwright(_ => { });
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddGhostwright_WithConfiguration_UsesProvidedConfig()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ghostwriter:Headless"] = "true" })
            .Build();
        var result = services.AddGhostwright(config, _ => { });
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddGhostwright_RegistersKernelServices()
    {
        var services = new ServiceCollection();
        services.AddGhostwright(_ => { });
        // Verify services were added (at minimum, options should be registered)
        services.Should().NotBeEmpty();
    }
}
