using Ghost.Contracts.Simulation;
using Ghost.Contracts.Social;
using Ghost.Platform.X.Internal;
using Ghost.Platform.X.MultiAccount;
using Ghost.Platform.X.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Ghost.Platform.X.Tests;

public class XExtensionTests
{
    [Fact]
    public void XExtension_Name_ReturnsGhostPlatformX()
    {
        // Arrange
        var extension = new XExtension();

        // Assert
        Assert.Equal("Ghost.Platform.X", extension.Name);
    }

    [Fact]
    public void XExtension_Version_Returns1_0_0()
    {
        // Arrange
        var extension = new XExtension();

        // Assert
        Assert.Equal(new Version(1, 0, 0), extension.Version);
    }

    [Fact]
    public void XExtension_ProvidedServices_ContainsExpectedTypes()
    {
        // Arrange
        var extension = new XExtension();

        // Assert
        Assert.Contains(typeof(ISocialClient), extension.ProvidedServices);
        Assert.Contains(typeof(IXPlatformSimulationValidator), extension.ProvidedServices);
        Assert.Contains(typeof(IXMetricsService), extension.ProvidedServices);
        Assert.Contains(typeof(IXWebhookService), extension.ProvidedServices);
        Assert.Contains(typeof(IXAccountManager), extension.ProvidedServices);
        Assert.Equal(5, extension.ProvidedServices.Count);
    }

    [Fact]
    public void XExtension_RequiredServices_ContainsBrowserSession()
    {
        // Arrange
        var extension = new XExtension();

        // Assert
        Assert.Contains(typeof(IBrowserSession), extension.RequiredServices);
        Assert.Single(extension.RequiredServices);
    }

    [Fact]
    public void ConfigureServices_RegistersXOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var extension = new XExtension();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        extension.ConfigureServices(services, configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetService<Microsoft.Extensions.Options.IOptions<XOptions>>();
        Assert.NotNull(options);
    }

    [Fact]
    public void ConfigureServices_RegistersXAuthenticator()
    {
        // Arrange
        var services = new ServiceCollection();
        var extension = new XExtension();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        extension.ConfigureServices(services, configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var authenticatorDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(XAuthenticator) &&
            s.Lifetime == ServiceLifetime.Scoped);
        Assert.NotNull(authenticatorDescriptor);
    }

    [Fact]
    public void ConfigureServices_RegistersXThreadComposer()
    {
        // Arrange
        var services = new ServiceCollection();
        var extension = new XExtension();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        extension.ConfigureServices(services, configuration);

        // Assert
        var composerDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(XThreadComposer) &&
            s.Lifetime == ServiceLifetime.Scoped);
        Assert.NotNull(composerDescriptor);
    }

    [Fact]
    public void ConfigureServices_RegistersXSimulationValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        var extension = new XExtension();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        extension.ConfigureServices(services, configuration);

        // Assert
        var validatorDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(XSimulationValidator) &&
            s.Lifetime == ServiceLifetime.Scoped);
        Assert.NotNull(validatorDescriptor);
    }

    [Fact]
    public void ConfigureServices_RegistersISocialClient()
    {
        // Arrange
        var services = new ServiceCollection();
        var extension = new XExtension();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        extension.ConfigureServices(services, configuration);

        // Assert
        var clientDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(ISocialClient) &&
            s.ImplementationType == typeof(XSocialClient) &&
            s.Lifetime == ServiceLifetime.Scoped);
        Assert.NotNull(clientDescriptor);
    }

    [Fact]
    public void ConfigureServices_RegistersIXPlatformSimulationValidator()
    {
        // Arrange
        var services = new ServiceCollection();
        var extension = new XExtension();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        extension.ConfigureServices(services, configuration);

        // Assert
        var validatorDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IXPlatformSimulationValidator) &&
            s.Lifetime == ServiceLifetime.Scoped);
        Assert.NotNull(validatorDescriptor);
    }

    [Fact]
    public void ConfigureServices_RegistersXPostContentSplitter_AsSingleton()
    {
        // Arrange
        var services = new ServiceCollection();
        var extension = new XExtension();
        var configuration = new ConfigurationBuilder().Build();

        // Act
        extension.ConfigureServices(services, configuration);

        // Assert
        var splitterDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(XPostContentSplitter) &&
            s.Lifetime == ServiceLifetime.Singleton);
        Assert.NotNull(splitterDescriptor);
    }

    [Fact]
    public void AddXPlatform_ExtensionMethod_RegistersServices()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddXPlatform();

        // Assert
        var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetService<Microsoft.Extensions.Options.IOptions<XOptions>>());
    }

    [Fact]
    public void AddXPlatform_WithConfiguration_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["X:BaseUrl"] = "https://test.x.com",
                ["X:MaxRetries"] = "5"
            })
            .Build();

        // Act
        services.AddXPlatform(configuration);

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<XOptions>>().Value;
        Assert.Equal("https://test.x.com", options.BaseUrl);
        Assert.Equal(5, options.MaxRetries);
    }

    [Fact]
    public void AddXPlatform_WithConfigureAction_AppliesOptions()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddXPlatform(options =>
        {
            options.BaseUrl = "https://custom.x.com";
            options.MaxRetries = 7;
        });

        // Assert
        var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<XOptions>>().Value;
        Assert.Equal("https://custom.x.com", options.BaseUrl);
        Assert.Equal(7, options.MaxRetries);
    }
}
