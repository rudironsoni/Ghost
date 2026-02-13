using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.LinkedIn;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInPluginParityTests
{
    [Fact]
    public void ConfigureServices_ShouldRegisterLinkedInJobClient_ForApiLinkedInPath()
    {
        var plugin = new LinkedInPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        plugin.ConfigureServices(services, configuration);

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(LinkedInJobClient) &&
            sd.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void ConfigureServices_ShouldRegisterUnkeyedIJobClient_ForApiJobsSearchPath()
    {
        var plugin = new LinkedInPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        plugin.ConfigureServices(services, configuration);

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IJobClient) &&
            !sd.IsKeyedService &&
            sd.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void ConfigureServices_ShouldRegisterKeyedIJobClient_ForWorkerPath()
    {
        var plugin = new LinkedInPlugin();
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder().Build();

        plugin.ConfigureServices(services, configuration);

        services.Should().Contain(sd =>
            sd.ServiceType == typeof(IJobClient) &&
            sd.IsKeyedService &&
            object.Equals(sd.ServiceKey, "linkedin") &&
            sd.Lifetime == ServiceLifetime.Scoped);
    }
}
