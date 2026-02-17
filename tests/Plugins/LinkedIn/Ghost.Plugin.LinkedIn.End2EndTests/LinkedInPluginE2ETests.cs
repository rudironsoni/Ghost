using global::Ghost;
using global::Ghost.Contracts.Jobs;
using global::Ghost.Contracts.News;
using global::Ghost.Contracts.Social;
using global::Ghost.Hosting;
using Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Testing.Contracts;
using Ghost.Testing.Fixtures;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

// NSubstitute
using NSubstitute;

namespace Ghost.Plugin.LinkedIn.End2EndTests;

/// <summary>
/// End-to-End tests for LinkedIn Plugin.
/// Tests the complete plugin lifecycle including setup and teardown.
/// </summary>
[Collection("LinkedInEnd2End")]
[Trait("Category", "End2End")]
public sealed class LinkedInPluginE2ETests : IAsyncLifetime, IClassFixture<LinkedInE2EFixture>
{
    private readonly LinkedInE2EFixture _fixture;
    private readonly RealBrowserFixture _browserFixture;

    public LinkedInPluginE2ETests(LinkedInE2EFixture fixture, RealBrowserFixture browserFixture)
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
        var plugin = new LinkedInPlugin();

        // Act
        string name = plugin.Name;

        // Assert
        Assert.Equal("LinkedIn", name);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_Version_ReturnsValidVersion()
    {
        // Arrange
        var plugin = new LinkedInPlugin();

        // Act
        Version version = plugin.Version;

        // Assert
        Assert.NotNull(version);
        Assert.Equal(1, version.Major);
        Assert.Equal(0, version.Minor);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_ProvidedServices_ContainsExpectedTypes()
    {
        // Arrange
        var plugin = new LinkedInPlugin();

        // Act
        IReadOnlyList<Type> providedServices = plugin.ProvidedServices;

        // Assert
        Assert.Contains(typeof(ISocialClient), providedServices);
        Assert.Contains(typeof(IJobClient), providedServices);
        Assert.Contains(typeof(INewsClient), providedServices);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_RequiredServices_ContainsIBrowserSession()
    {
        // Arrange
        var plugin = new LinkedInPlugin();

        // Act
        IReadOnlyList<Type> requiredServices = plugin.RequiredServices;

        // Assert
        Assert.Contains(typeof(Ghost.IBrowserSession), requiredServices);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersLinkedInJobClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true",
                ["Ghost:Extensions:LinkedIn:BaseUrl"] = "https://www.linkedin.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Add required services
        services.AddSingleton(Substitute.For<Ghost.IBrowserSession>());
        services.AddSingleton<JavaScriptAdapter>();
        services.AddSingleton<EntityParser>();

        var plugin = new LinkedInPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        IJobClient? client = serviceProvider.GetService<IJobClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersLinkedInSocialClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true",
                ["Ghost:Extensions:LinkedIn:BaseUrl"] = "https://www.linkedin.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Add required services
        services.AddSingleton(Substitute.For<Ghost.IBrowserSession>());

        var plugin = new LinkedInPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        ISocialClient? client = serviceProvider.GetService<ISocialClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersLinkedInNewsClient()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Add required services
        services.AddSingleton(Substitute.For<Ghost.IBrowserSession>());

        var plugin = new LinkedInPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        INewsClient? client = serviceProvider.GetService<INewsClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersSessionPool()
    {
        // Arrange
        IConfigurationRoot configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true",
                ["Ghost:Extensions:LinkedIn:SessionPool:MaxSize"] = "5"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Mock required services
#pragma warning disable CS0618 // IGhostKernel may be obsolete
        global::Ghost.Kernel.IGhostKernel mockKernel = Substitute.For<global::Ghost.Kernel.IGhostKernel>();
#pragma warning restore CS0618
        services.AddSingleton(mockKernel);
        services.AddSingleton(Substitute.For<IProxyProvider>());

        var plugin = new LinkedInPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert - SessionPool is registered
        ServiceProvider serviceProvider = services.BuildServiceProvider();
        // SessionPool is internal, verify by checking configuration was bound
        IOptions<LinkedInSessionPoolOptions>? options = serviceProvider.GetService<IOptions<LinkedInSessionPoolOptions>>();
        Assert.NotNull(options);
        Assert.Equal(5, options.Value.MaxSize);
    }
}
