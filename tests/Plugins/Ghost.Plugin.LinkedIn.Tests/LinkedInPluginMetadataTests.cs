using System;
using FluentAssertions;
using Ghost.Plugin.LinkedIn;
using Ghost.Testing.Reliability;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInPluginMetadataTests
{
    [Trait("Category", "Unit")]
    [TestTimeout(10000)]
    [Fact]
    public void Name_ShouldBeLinkedIn()
    {
        // Arrange
        var plugin = new LinkedInPlugin();

        // Act & Assert
        plugin.Name.Should().Be("LinkedIn");
    }

    [Trait("Category", "Unit")]
    [TestTimeout(10000)]
    [Fact]
    public void Version_ShouldBe1_0_0()
    {
        // Arrange
        var plugin = new LinkedInPlugin();

        // Act & Assert
        plugin.Version.Should().Be(new Version(1, 0, 0));
    }

    [Trait("Category", "Unit")]
    [TestTimeout(10000)]
    [Fact]
    public void ProvidedServices_ShouldIncludeExpectedServices()
    {
        // Arrange
        var plugin = new LinkedInPlugin();

        // Act & Assert
        plugin.ProvidedServices.Should().Contain(typeof(Ghost.Contracts.Social.ISocialClient));
        plugin.ProvidedServices.Should().Contain(typeof(Ghost.Contracts.Jobs.IJobClient));
        plugin.ProvidedServices.Should().Contain(typeof(Ghost.Contracts.News.INewsClient));
    }

    [Trait("Category", "Unit")]
    [TestTimeout(10000)]
    [Fact]
    public void RequiredServices_ShouldIncludeBrowserSession()
    {
        // Arrange
        var plugin = new LinkedInPlugin();

        // Act & Assert
        plugin.RequiredServices.Should().Contain(typeof(Ghost.IBrowserSession));
    }
}
