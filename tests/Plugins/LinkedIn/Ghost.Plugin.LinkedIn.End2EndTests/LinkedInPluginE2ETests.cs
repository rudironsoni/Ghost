using Ghost.Contracts.Jobs;
using Ghost.Contracts.News;
using Ghost.Contracts.Social;
using Ghost.Hosting;
using Ghost.Plugin.LinkedIn.End2EndTests.Fixtures;
using Ghost.Plugin.LinkedIn.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ghost.Plugin.LinkedIn.End2EndTests;

/// <summary>
/// End-to-End tests for LinkedIn Plugin DI registration and lifecycle.
/// </summary>
[Collection("LinkedInEnd2End")]
[Trait("Category", "End2End")]
public sealed class LinkedInPluginE2ETests
{
    private readonly LinkedInE2EFixture _fixture;

    public LinkedInPluginE2ETests(LinkedInE2EFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void Plugin_Name_ReturnsExpectedValue()
    {
        // Arrange
        var plugin = new LinkedInPlugin();

        // Act
        var name = plugin.Name;

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
        var version = plugin.Version;

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
        var providedServices = plugin.ProvidedServices;

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
        var requiredServices = plugin.RequiredServices;

        // Assert
        Assert.Contains(typeof(Ghost.IBrowserSession), requiredServices);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersLinkedInJobClient()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true",
                ["Ghost:Extensions:LinkedIn:BaseUrl"] = "https://www.linkedin.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Add required services
        services.AddSingleton<NSubstitute.Substitute.For<Ghost.IBrowserSession>>();
        services.AddSingleton<JavaScriptAdapter>();
        services.AddSingleton<EntityParser>();

        var plugin = new LinkedInPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<IJobClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersLinkedInSocialClient()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true",
                ["Ghost:Extensions:LinkedIn:BaseUrl"] = "https://www.linkedin.com"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Add required services
        services.AddSingleton<NSubstitute.Substitute.For<Ghost.IBrowserSession>>();

        var plugin = new LinkedInPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<ISocialClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersLinkedInNewsClient()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Add required services
        services.AddSingleton<NSubstitute.Substitute.For<Ghost.IBrowserSession>>();

        var plugin = new LinkedInPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var client = serviceProvider.GetService<INewsClient>();
        Assert.NotNull(client);
    }

    [Fact]
    [Trait("TestType", "End2End")]
    public void ConfigureServices_RegistersSessionPool()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["Ghost:Extensions:LinkedIn:Enabled"] = "true",
                ["Ghost:Extensions:LinkedIn:SessionPool:MaxSessions"] = "5"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();

        // Mock required services
        var mockKernel = NSubstitute.Substitute.For<Ghost.Kernel.IGhostKernel>();
        services.AddSingleton(mockKernel);
        services.AddSingleton<NSubstitute.Substitute.For<Ghost.IProxyProvider>>();

        var plugin = new LinkedInPlugin();

        // Act
        plugin.ConfigureServices(services, configuration);

        // Assert - SessionPool is registered
        var serviceProvider = services.BuildServiceProvider();
        // SessionPool is internal, verify by checking configuration was bound
        var options = serviceProvider.GetService<IOptions<LinkedInSessionPoolOptions>>();
        Assert.NotNull(options);
        Assert.Equal(5, options.Value.MaxSessions);
    }
}
