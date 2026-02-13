using System;
using System.Collections.Generic;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Plugin.LinkedIn;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInPluginRegistrationTests
{
    [Trait("Category", "Unit")]
    [TestTimeout(10000)]
    [Fact]
    public void ConfigureServices_ShouldRegisterReadinessCheckService()
    {
        // Arrange
        var plugin = new LinkedInPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        services.Should().ContainSingle(sd => sd.ServiceType == typeof(ILinkedInPluginReadinessCheck));
    }

    [Trait("Category", "Unit")]
    [TestTimeout(10000)]
    [Fact]
    public void ConfigureServices_ShouldRegisterCapabilitiesService()
    {
        // Arrange
        var plugin = new LinkedInPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        services.Should().ContainSingle(sd => sd.ServiceType == typeof(LinkedInPluginCapabilities));
    }

    [Trait("Category", "Unit")]
    [TestTimeout(10000)]
    [Fact]
    public void ConfigureServices_ShouldRegisterKeyedJobClient()
    {
        // Arrange
        var plugin = new LinkedInPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        // Verify that the keyed service is registered
        var keyedDescriptor = services.FirstOrDefault(sd =>
            sd.ServiceType == typeof(IJobClient) &&
            sd.IsKeyedService &&
            sd.ServiceKey?.ToString() == "linkedin");
        keyedDescriptor.Should().NotBeNull();
        keyedDescriptor!.Lifetime.Should().Be(ServiceLifetime.Scoped);
    }

    [Trait("Category", "Unit")]
    [TestTimeout(10000)]
    [Fact]
    public void Capabilities_ShouldHaveExpectedValues()
    {
        // Arrange
        var plugin = new LinkedInPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        plugin.ConfigureServices(services, configuration);
        var serviceProvider = services.BuildServiceProvider();
        var capabilities = serviceProvider.GetRequiredService<LinkedInPluginCapabilities>();

        // Assert
        capabilities.RequiresBrowser.Should().BeTrue();
        capabilities.RequiresProxy.Should().BeFalse();
        capabilities.SupportsJobs.Should().BeTrue();
        capabilities.SupportsSocial.Should().BeTrue();
        capabilities.SupportsNews.Should().BeTrue();
    }
}
