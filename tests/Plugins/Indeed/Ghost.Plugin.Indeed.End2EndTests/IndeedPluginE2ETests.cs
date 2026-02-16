using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Plugin.Indeed.End2EndTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.Indeed.End2EndTests;

/// <summary>
/// End-to-End tests for Indeed Plugin DI registration and lifecycle.
/// </summary>
[Collection("IndeedEnd2End")]
[Trait("Category", "End2End")]
public sealed class IndeedPluginE2ETests
{
    private readonly IndeedE2EFixture _fixture;

    public IndeedPluginE2ETests(IndeedE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_Name_ReturnsExpectedValue()
    {
        // Arrange
        var plugin = new IndeedPlugin();

        // Act
        string name = plugin.Name;

        // Assert
        Assert.Equal("Indeed", name);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_Version_ReturnsValidVersion()
    {
        // Arrange
        var plugin = new IndeedPlugin();

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
        var plugin = new IndeedPlugin();

        // Act
        IReadOnlyList<Type> providedServices = plugin.ProvidedServices;

        // Assert
        Assert.Contains(typeof(IJobClient), providedServices);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersIndeedJobClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:Indeed:Enabled"] = "true",
                ["Ghost:Extensions:Indeed:Country"] = "us",
                ["Ghost:Extensions:Indeed:BaseUrl"] = "https://www.indeed.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var plugin = new IndeedPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IJobClient? client = serviceProvider.GetService<IJobClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersIndeedApiClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:Indeed:Enabled"] = "true",
                ["Ghost:Extensions:Indeed:Country"] = "us"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var plugin = new IndeedPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        // ApiClient is internal, verify by checking service registration
        Assert.Contains(services, s => s.ServiceType.Name.Contains("IndeedApiClient"));
    }
}
