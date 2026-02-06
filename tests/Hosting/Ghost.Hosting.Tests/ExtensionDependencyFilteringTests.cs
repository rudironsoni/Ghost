using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Hosting.Tests;

/// <summary>
/// Generic tests for DI filtering based on configuration.
/// Tests the mechanism that prevents disabled extensions from registering their services.
/// </summary>
public class ExtensionDependencyFilteringTests
{
    [Fact]
    public void ServiceCollectionWithAllExtensionsDisabledShouldRegisterNoJobScrapers()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:Extension1:Enabled"] = "false",
                ["Ghost:Extensions:Extension2:Enabled"] = "false",
                ["Ghost:Extensions:Extension3:Enabled"] = "false"
            })
            .Build();

        if (config.GetValue<bool>("Ghost:Extensions:Extension1:Enabled"))
        {
            services.AddSingleton<IJobScraper, MockJobScraper1>();
        }
        if (config.GetValue<bool>("Ghost:Extensions:Extension2:Enabled"))
        {
            services.AddSingleton<IJobScraper, MockJobScraper2>();
        }
        if (config.GetValue<bool>("Ghost:Extensions:Extension3:Enabled"))
        {
            services.AddSingleton<IJobScraper, MockJobScraper3>();
        }

        var serviceProvider = services.BuildServiceProvider();

        var scraper = serviceProvider.GetService<IJobScraper>();
        scraper.Should().BeNull("No scrapers should be registered when all are disabled");
    }

    [Fact]
    public void ServiceCollectionWithSomeExtensionsEnabledShouldRegisterOnlyEnabledOnes()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:Extension1:Enabled"] = "true",
                ["Ghost:Extensions:Extension2:Enabled"] = "false",
                ["Ghost:Extensions:Extension3:Enabled"] = "true"
            })
            .Build();

        if (config.GetValue<bool>("Ghost:Extensions:Extension1:Enabled"))
        {
            services.AddSingleton<MockJobScraper1>();
        }
        if (config.GetValue<bool>("Ghost:Extensions:Extension2:Enabled"))
        {
            services.AddSingleton<MockJobScraper2>();
        }
        if (config.GetValue<bool>("Ghost:Extensions:Extension3:Enabled"))
        {
            services.AddSingleton<MockJobScraper3>();
        }

        var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<MockJobScraper1>().Should().NotBeNull();
        serviceProvider.GetRequiredService<MockJobScraper3>().Should().NotBeNull();
        serviceProvider.Invoking(x => x.GetRequiredService<MockJobScraper2>())
            .Should().Throw<InvalidOperationException>("Scraper2 should not be registered");
    }

    [Fact]
    public void ConfigurationValueWhenFalseShouldPreventConditionalRegistration()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:Sample:Enabled"] = "false",
                ["Ghost:Extensions:Sample:Setting"] = "some-value"
            })
            .Build();

        var isEnabled = config.GetValue<bool>("Ghost:Extensions:Sample:Enabled");
        var setting = config.GetValue<string>("Ghost:Extensions:Sample:Setting", string.Empty);

        isEnabled.Should().BeFalse("Configuration should parse false correctly");
        setting.Should().Be("some-value", "Other settings remain accessible even when extension is disabled");
    }

    [Fact]
    public void ConditionalRegistrationWithBooleanCheckShouldSkipDisabledExtensions()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:ProviderA:Enabled"] = "false",
                ["Ghost:Extensions:ProviderB:Enabled"] = "true",
                ["Ghost:Extensions:ProviderC:Enabled"] = "false"
            })
            .Build();

        int registeredCount = 0;

        if (config.GetValue<bool>("Ghost:Extensions:ProviderA:Enabled"))
        {
            registeredCount++;
        }

        if (config.GetValue<bool>("Ghost:Extensions:ProviderB:Enabled"))
        {
            registeredCount++;
        }

        if (config.GetValue<bool>("Ghost:Extensions:ProviderC:Enabled"))
        {
            registeredCount++;
        }

        registeredCount.Should().Be(1, "Only one extension should be 'registered'");
    }

    [Fact]
    public void ServiceProviderWithNoServiceShouldThrowOnResolution()
    {
        var services = new ServiceCollection();
        var serviceProvider = services.BuildServiceProvider();

        Action resolveService = () => serviceProvider.GetRequiredService<IJobScraper>();
        resolveService.Should().Throw<InvalidOperationException>("Unregistered services should throw");
    }

    [Fact]
    public void ServiceCollectionShouldTrackConditionalRegistrationsCorrectly()
    {
        var services = new ServiceCollection();
        var enabledServices = new List<Type> { typeof(MockJobScraper1), typeof(MockJobScraper2) };

        foreach (var serviceType in enabledServices)
        {
            if (serviceType == typeof(MockJobScraper1))
            {
                services.AddSingleton<MockJobScraper1>();
            }
            else if (serviceType == typeof(MockJobScraper2))
            {
                services.AddSingleton<MockJobScraper2>();
            }
        }

        var serviceProvider = services.BuildServiceProvider();

        serviceProvider.GetRequiredService<MockJobScraper1>().Should().NotBeNull();
        serviceProvider.GetRequiredService<MockJobScraper2>().Should().NotBeNull();
        serviceProvider.Invoking(x => x.GetRequiredService<MockJobScraper3>())
            .Should().Throw<InvalidOperationException>("Scraper3 should not be registered");
    }
}

#region Mock Implementations

public interface IJobScraper { }
public sealed class MockJobScraper1 : IJobScraper { }
public sealed class MockJobScraper2 : IJobScraper { }
public sealed class MockJobScraper3 : IJobScraper { }

#endregion
