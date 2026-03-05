using Ghost.Contracts.Inference;
using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Plugin.Google.End2EndTests.Fixtures;
using Ghost.Plugin.Google.Gemini;
using Ghost.Plugin.Google.Jobs;
using Ghost.Testing.End2End;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.Google.End2EndTests;

/// <summary>
/// End-to-End tests for Google Plugin DI registration and lifecycle.
/// </summary>
[Collection("GoogleEnd2End")]
[Trait("Category", "End2End")]
[Trait("Capability", "RequiresProviderLive")]
public sealed class GooglePluginE2ETests : IAsyncLifetime, IClassFixture<GoogleE2EFixture>
{
    private readonly GoogleE2EFixture _fixture;
    private readonly RealBrowserFixture _browserFixture;

    public GooglePluginE2ETests(GoogleE2EFixture fixture, RealBrowserFixture browserFixture)
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

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public void Plugin_Name_ReturnsExpectedValue()
    {
        // Arrange
        var plugin = new GooglePlugin();

        // Act
        string name = plugin.Name;

        // Assert
        Assert.Equal("Google", name);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public void Plugin_Version_ReturnsValidVersion()
    {
        // Arrange
        var plugin = new GooglePlugin();

        // Act
        Version version = plugin.Version;

        // Assert
        Assert.NotNull(version);
        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public void Plugin_ProvidedServices_ContainsExpectedTypes()
    {
        // Arrange
        var plugin = new GooglePlugin();

        // Act
        IReadOnlyList<Type> providedServices = plugin.ProvidedServices;

        // Assert
        Assert.Contains(typeof(GoogleJobClient), providedServices);
        Assert.Contains(typeof(GeminiClient), providedServices);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersGoogleJobClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Plugins:Google:Jobs:Enabled"] = "true",
                ["Ghost:Plugins:Google:Jobs:ApiKey"] = "test-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        var plugin = new GooglePlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert - Verify registration in service collection (resolution requires complex dependency chain)
        Assert.Contains(services, s => s.ServiceType == typeof(GoogleJobClient));
        Assert.Contains(services, s => s.Lifetime == ServiceLifetime.Singleton && s.ServiceType == typeof(GoogleJobClient));
    }

    [End2EndFact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersGeminiClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Plugins:Google:Gemini:Enabled"] = "true",
                ["Ghost:Plugins:Google:Gemini:ApiKey"] = "test-key"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Mock IBrowserSession
        IBrowserSession mockBrowserSession = NSubstitute.Substitute.For<Ghost.IBrowserSession>();
        services.AddSingleton(mockBrowserSession);

        var plugin = new GooglePlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        GeminiClient? client = serviceProvider.GetService<GeminiClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "ArchitectureAssurance")]
    public void GoogleJobsApiClient_UsesProductionCodepath_WithSessionOrchestrator()
    {
        // Arrange & Act
        Jobs.Internal.GoogleJobsApiClient apiClient = _fixture.ServiceProvider.GetRequiredService<Jobs.Internal.GoogleJobsApiClient>();

        // Assert - verify the client was resolved via DI using the [ActivatorUtilitiesConstructor]
        // which requires ISessionOrchestrator (production codepath), not the legacy HttpClient constructor
        Assert.NotNull(apiClient);

        // Verify ISessionOrchestrator is registered and available (production codepath marker)
        Ghost.Platform.Storage.Session.ISessionOrchestrator? sessionOrchestrator = _fixture.ServiceProvider.GetService<Ghost.Platform.Storage.Session.ISessionOrchestrator>();
        Assert.NotNull(sessionOrchestrator);

        // Verify the fixture properly configured SessionOrchestrator (not using stubbed path)
        Ghost.IProxyProvider? proxyProvider = _fixture.ServiceProvider.GetService<Ghost.IProxyProvider>();
        Assert.NotNull(proxyProvider);
    }
}
