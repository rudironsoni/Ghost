using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Plugin.Glassdoor.Tests;

public class GlassdoorExtensionTests
{
    [Fact]
    public void NameShouldContainGlassdoor()
    {
        var ext = new Ghost.Plugin.Glassdoor.GlassdoorExtension();
        ext.Name.ToLowerInvariant().Should().Contain("glassdoor");
    }

    [Fact]
    public void ConfigureServicesDoesNotThrow()
    {
        var ext = new Ghost.Plugin.Glassdoor.GlassdoorExtension();
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        Action act = () => ext.ConfigureServices(services, config);
        act.Should().NotThrow();
    }
}
