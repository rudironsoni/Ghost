using System;
using FluentAssertions;
using Ghost.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ghost.Worker.Tests;

/// <summary>
/// Tests for WorkerConfiguration.
/// </summary>
public sealed class WorkerConfigurationTests
{
    [Fact]
    public void WorkerConfiguration_Defaults_ShutdownTimeout_Is10Seconds()
    {
        // Arrange & Act
        var config = new WorkerConfiguration();

        // Assert
        config.ShutdownTimeoutSeconds.Should().Be(10);
    }

    [Fact]
    public void WorkerConfiguration_CanBeConfigured_FromConfiguration()
    {
        // Arrange
        var services = new ServiceCollection();
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new[]
            {
                new KeyValuePair<string, string?>("Worker:ShutdownTimeoutSeconds", "30"),
                new KeyValuePair<string, string?>("Worker:MaxConcurrentJobs", "10")
            })
            .Build();

        services.Configure<WorkerConfiguration>(configuration.GetSection("Worker"));

        ServiceProvider serviceProvider = services.BuildServiceProvider();

        // Act
        WorkerConfiguration options = serviceProvider.GetRequiredService<IOptions<WorkerConfiguration>>().Value;

        // Assert
        options.ShutdownTimeoutSeconds.Should().Be(30);
        options.MaxConcurrentJobs.Should().Be(10);
    }

    [Fact]
    public void WorkerConfiguration_ShutdownTimeout_CanBeOverridden()
    {
        // Arrange & Act
        var config = new WorkerConfiguration
        {
            ShutdownTimeoutSeconds = 5
        };

        // Assert
        config.ShutdownTimeoutSeconds.Should().Be(5);
    }

    [Fact]
    public void WorkerConfiguration_Defaults_AreReasonable()
    {
        // Arrange & Act
        var config = new WorkerConfiguration();

        // Assert
        config.MaxConcurrentJobs.Should().Be(5);
        config.PollIntervalMs.Should().Be(1000);
        config.ResultsExpirationHours.Should().Be(24);
        config.ShutdownTimeoutSeconds.Should().Be(10);
        config.RedisQueueKey.Should().Be("ghost:jobs:queue");
        config.RedisConnectionString.Should().Be("localhost:6379");
    }
}
