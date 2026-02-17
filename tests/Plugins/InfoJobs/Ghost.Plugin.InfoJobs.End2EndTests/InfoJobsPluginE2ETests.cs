using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Plugin.InfoJobs.End2EndTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ghost.Plugin.InfoJobs.End2EndTests;

/// <summary>
/// End-to-End tests for InfoJobs Plugin DI registration and lifecycle.
/// </summary>
[Collection("InfoJobsEnd2End")]
[Trait("Category", "End2End")]
public sealed class InfoJobsPluginE2ETests
{
    private readonly InfoJobsE2EFixture _fixture;

    public InfoJobsPluginE2ETests(InfoJobsE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_Name_ReturnsExpectedValue()
    {
        // Arrange
        var plugin = new InfoJobsPlugin();

        // Act
        string name = plugin.Name;

        // Assert
        Assert.Equal("InfoJobs", name);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_Version_ReturnsValidVersion()
    {
        // Arrange
        var plugin = new InfoJobsPlugin();

        // Act
        Version version = plugin.Version;

        // Assert
        Assert.NotNull(version);
        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_ProvidedServices_ContainsIJobClient()
    {
        // Arrange
        var plugin = new InfoJobsPlugin();

        // Act
        IReadOnlyList<Type> providedServices = plugin.ProvidedServices;

        // Assert
        Assert.Contains(typeof(IJobClient), providedServices);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersInfoJobClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:InfoJobs:Enabled"] = "true",
                ["Ghost:Extensions:InfoJobs:ApiKey"] = "test-key",
                ["Ghost:Extensions:InfoJobs:ClientId"] = "test-client-id",
                ["Ghost:Extensions:InfoJobs:ClientSecret"] = "test-secret"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var plugin = new InfoJobsPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IJobClient? client = serviceProvider.GetService<IJobClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersInfoJobsOptions()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:InfoJobs:Enabled"] = "true",
                ["Ghost:Extensions:InfoJobs:ApiKey"] = "test-key",
                ["Ghost:Extensions:InfoJobs:ClientId"] = "test-client-id",
                ["Ghost:Extensions:InfoJobs:ClientSecret"] = "test-client-secret",
                ["Ghost:Extensions:InfoJobs:BaseUrl"] = "https://api.infojobs.net"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var plugin = new InfoJobsPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IOptions<InfoJobsOptions>? options = serviceProvider.GetService<IOptions<InfoJobsOptions>>();
        Assert.NotNull(options);
        Assert.True(options.Value.Enabled);
        Assert.Equal("test-key", options.Value.ApiKey);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersPluginCapabilities()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:InfoJobs:Enabled"] = "true",
                ["Ghost:Plugins:InfoJobs:RegisterReadinessServices"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var plugin = new InfoJobsPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        InfoJobsPluginCapabilities? capabilities = serviceProvider.GetService<InfoJobsPluginCapabilities>();
        Assert.NotNull(capabilities);
        Assert.True(capabilities.SupportsJobs);
        Assert.False(capabilities.SupportsSocial);
        Assert.False(capabilities.SupportsNews);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersReadinessCheck()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:InfoJobs:Enabled"] = "true",
                ["Ghost:Plugins:InfoJobs:RegisterReadinessServices"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var plugin = new InfoJobsPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IInfoJobsPluginReadinessCheck? readinessCheck = serviceProvider.GetService<IInfoJobsPluginReadinessCheck>();
        Assert.NotNull(readinessCheck);
    }
}
