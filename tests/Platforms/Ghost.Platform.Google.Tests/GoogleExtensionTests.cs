using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Google.Tests;

[Collection("GooglePlatformTests")]
public class GoogleExtensionTests
{
    [Fact]
    public void NameShouldContainGoogle()
    {
        var ext = new GoogleExtension();
        ext.Name.ToLowerInvariant().Should().Contain("google");
    }

    [Fact]
    public void ConfigureServicesDoesNotThrow()
    {
        var ext = new GoogleExtension();
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        Action act = () => ext.ConfigureServices(services, config);
        act.Should().NotThrow();
    }
}
