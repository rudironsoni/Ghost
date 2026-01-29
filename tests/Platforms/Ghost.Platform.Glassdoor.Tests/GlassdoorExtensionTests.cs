using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Glassdoor.Tests;

public class GlassdoorExtensionTests
{
    [Fact]
    public void Name_ShouldContainGlassdoor()
    {
        var ext = new Ghost.Platform.Glassdoor.GlassdoorExtension();
        ext.Name.ToLowerInvariant().Should().Contain("glassdoor");
    }

    [Fact]
    public void ConfigureServices_DoesNotThrow()
    {
        var ext = new Ghost.Platform.Glassdoor.GlassdoorExtension();
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder().Build();
        Action act = () => ext.ConfigureServices(services, config);
        act.Should().NotThrow();
    }
}
