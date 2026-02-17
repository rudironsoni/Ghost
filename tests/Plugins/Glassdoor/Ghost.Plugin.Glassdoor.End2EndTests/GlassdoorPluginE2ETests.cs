using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Plugin.Glassdoor.End2EndTests.Fixtures;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.Glassdoor.End2EndTests;

/// <summary>
/// End-to-End tests for Glassdoor Plugin DI registration and lifecycle.
/// </summary>
[Collection("GlassdoorEnd2End")]
[Trait("Category", "End2End")]
public sealed class GlassdoorPluginE2ETests : IAsyncLifetime, IClassFixture<GlassdoorE2EFixture>
{
    private readonly GlassdoorE2EFixture _fixture;
    private readonly RealBrowserFixture _browserFixture;

    public GlassdoorPluginE2ETests(GlassdoorE2EFixture fixture, RealBrowserFixture browserFixture)
    {
        _fixture = fixture;
        _browserFixture = browserFixture;
    }

    public async Task InitializeAsync()
    {
        await _fixture.InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        await _fixture.DisposeAsync();
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_Name_ReturnsExpectedValue()
    {
        // Arrange
        var plugin = new GlassdoorPlugin();

        // Act
        string name = plugin.Name;

        // Assert
        Assert.Equal("Glassdoor", name);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_Version_ReturnsValidVersion()
    {
        // Arrange
        var plugin = new GlassdoorPlugin();

        // Act
        Version version = plugin.Version;

        // Assert
        Assert.NotNull(version);
        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_ProvidesIJobClient()
    {
        // Arrange
        var plugin = new GlassdoorPlugin();

        // Act
        IReadOnlyList<Type> providedServices = plugin.ProvidedServices;

        // Assert
        Assert.Contains(typeof(IJobClient), providedServices);
    }

    [Fact(Skip = "Requires ILogger<GlassdoorJobClient> to be registered")]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersGlassdoorJobClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:Glassdoor:Enabled"] = "true",
                ["Ghost:Extensions:Glassdoor:BaseUrl"] = "https://www.glassdoor.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var plugin = new GlassdoorPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IJobClient? client = serviceProvider.GetService<IJobClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersGlassdoorApiClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:Glassdoor:Enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        var plugin = new GlassdoorPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        // ApiClient is internal, but we can verify the service collection has the expected registrations
        Assert.Contains(services, s => s.ServiceType.Name.Contains("GlassdoorApiClient"));
    }
}
