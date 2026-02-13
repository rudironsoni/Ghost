using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Ghost.Core;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Scheduler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ghost.Hosting.Tests;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGhostNullServicesThrowsArgumentNullException()
    {
        IServiceCollection? services = null;
        var act = () => Ghost.Hosting.ServiceCollectionExtensions.AddGhost(services!, _ => { });
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGhostNullConfigureThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        var act = () => services.AddGhost(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AddGhostValidConfigReturnsServices()
    {
        var services = new ServiceCollection();
        var result = services.AddGhost(_ => { });
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddGhostWithConfigurationUsesProvidedConfig()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Ghost:Headless"] = "true" })
            .Build();
        var result = services.AddGhost(config, _ => { });
        result.Should().BeSameAs(services);
    }

    [Fact]
    public void AddGhostRegistersKernelServices()
    {
        var services = new ServiceCollection();
        services.AddGhost(_ => { });
        // Verify services were added (at minimum, options should be registered)
        services.Should().NotBeEmpty();
    }

    [Fact]
    public async Task AddGhostCanBuildProviderWithoutSynchronousKernelInitialization()
    {
        var services = new ServiceCollection();
        services.AddGhost(_ => { });

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        var kernel = provider.GetRequiredService<IGhostKernel>();
        kernel.Should().NotBeNull();

        await using var scope = provider.CreateAsyncScope();
        var browserSession = scope.ServiceProvider.GetRequiredService<Ghost.IBrowserSession>();
        browserSession.Should().NotBeNull();
    }

    [Fact]
    public async Task AddGhostRegistersEngineRuntimeServices()
    {
        var services = new ServiceCollection();
        services.AddGhost(_ => { });

        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });

        provider.GetRequiredService<IGhostEngine>().Should().NotBeNull();
        provider.GetRequiredService<IRequestScheduler>().Should().NotBeNull();

        var hostedServices = provider.GetServices<IHostedService>().ToList();
        var warmupService = hostedServices.FirstOrDefault(x => x.GetType().Name == "GhostEngineWarmupHostedService");
        warmupService.Should().NotBeNull();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await warmupService!.StartAsync(cts.Token);
        await warmupService.StopAsync(cts.Token);
    }

    [Fact]
    public void AddGhostEngineOptionsValidationFailsOnInvalidConfiguration()
    {
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Engine:MaxInFlight"] = "0",
                ["Ghost:Engine:MaxPendingItems"] = "10"
            })
            .Build();

        services.AddGhost(config, _ => { });

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<Ghost.Engine.Engine.GhostEngineOptions>>();

        var act = () => _ = options.Value;
        act.Should().Throw<OptionsValidationException>();
    }
}
