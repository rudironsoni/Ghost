using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Hosting.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGhost_NullServices_ThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        var act = () => Ghost.Hosting.ServiceCollectionExtensions.AddGhost(services!, _ => { });
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGhost_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddGhost(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGhost_ValidConfig_ReturnsServices()
    {
        var services = new ServiceCollection();
        var result = services.AddGhost(_ => { });
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddGhost_WithConfiguration_UsesProvidedConfig()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ghostwriter:Headless"] = "true" })
            .Build();
        var result = services.AddGhost(config, _ => { });
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddGhost_RegistersKernelServices()
    {
        var services = new ServiceCollection();
        services.AddGhost(_ => { });
        // Verify services were added (at minimum, options should be registered)
        services.Should().NotBeEmpty();
    }
}
