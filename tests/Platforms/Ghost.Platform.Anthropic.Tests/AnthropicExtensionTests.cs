using System;
using FluentAssertions;
using Xunit;

namespace Ghost.Platform.Anthropic.Tests;

[Trait("Category", "Unit")]
public class AnthropicExtensionTests
{
    [Fact]
    public void NameShouldContainAnthropic()
    {
        var ext = new AnthropicExtension();
        ext.Name.Should().NotBeNullOrEmpty();
        ext.Name.ToLowerInvariant().Should().Contain("anthropic");
    }

    [Fact]
    public void VersionShouldBeSet()
    {
        var ext = new AnthropicExtension();
        ext.Version.Should().NotBeNull();
        ext.Version.Major.Should().BeGreaterOrEqualTo(1);
    }

    [Fact]
    public void ProvidedServicesAndRequiredServicesAreConsistent()
    {
        var ext = new AnthropicExtension();
        ext.ProvidedServices.Should().NotBeNull();
        ext.RequiredServices.Should().NotBeNull();
    }

    [Fact]
    public void ConfigureServicesDoesNotThrow()
    {
        var ext = new AnthropicExtension();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
        Action act = () => ext.ConfigureServices(services, configuration);
        act.Should().NotThrow();
    }
}
