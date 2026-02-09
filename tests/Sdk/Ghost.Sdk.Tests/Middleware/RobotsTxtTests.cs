using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class RobotsTxtTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void CanFetch_WithNoRules_ReturnsTrue()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();

        // Act
        var result = robotsTxt.CanFetch("/admin", "TestBot");

        // Assert
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void CanFetch_WithMatchingDisallow_ReturnsFalse()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();
        var rules = new UserAgentRules();
        rules.AddDisallow("/admin");
        robotsTxt.AddRules("*", rules);

        // Act
        var result = robotsTxt.CanFetch("/admin", "TestBot");

        // Assert
        result.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void CanFetch_WithMatchingAllow_ReturnsTrue()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();
        var rules = new UserAgentRules();
        rules.AddDisallow("/admin");
        rules.AddAllow("/admin/public");
        robotsTxt.AddRules("*", rules);

        // Act
        var result = robotsTxt.CanFetch("/admin/public", "TestBot");

        // Assert
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void GetCrawlDelay_WithNoRules_ReturnsNull()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();

        // Act
        var result = robotsTxt.GetCrawlDelay("TestBot");

        // Assert
        result.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void GetCrawlDelay_WithUserAgentSpecific_ReturnsSpecificDelay()
    {
        // Arrange
        var robotsTxt = new RobotsTxt { CrawlDelay = 1.0 };
        var rules = new UserAgentRules { CrawlDelay = 5.0 };
        robotsTxt.AddRules("TestBot", rules);

        // Act
        var result = robotsTxt.GetCrawlDelay("TestBot");

        // Assert
        result.Should().Be(5.0);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void GetCrawlDelay_WithGlobalOnly_ReturnsGlobalDelay()
    {
        // Arrange
        var robotsTxt = new RobotsTxt { CrawlDelay = 2.0 };

        // Act
        var result = robotsTxt.GetCrawlDelay("TestBot");

        // Assert
        result.Should().Be(2.0);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void AddRules_WithNullUserAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();
        var rules = new UserAgentRules();

        // Act
        var act = () => robotsTxt.AddRules(null!, rules);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void AddRules_WithNullRules_ThrowsArgumentNullException()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();

        // Act
        var act = () => robotsTxt.AddRules("TestBot", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void CanFetch_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();

        // Act
        var act = () => robotsTxt.CanFetch(null!, "TestBot");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void CanFetch_WithNullUserAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();

        // Act
        var act = () => robotsTxt.CanFetch("/admin", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void GetCrawlDelay_WithNullUserAgent_ThrowsArgumentNullException()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();

        // Act
        var act = () => robotsTxt.GetCrawlDelay(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Sitemaps_CanBeAdded()
    {
        // Arrange
        var robotsTxt = new RobotsTxt();

        // Act
        robotsTxt.Sitemaps.Add("https://example.com/sitemap.xml");
        robotsTxt.Sitemaps.Add("https://example.com/sitemap2.xml");

        // Assert
        robotsTxt.Sitemaps.Should().HaveCount(2);
        robotsTxt.Sitemaps.Should().Contain("https://example.com/sitemap.xml");
    }
}

public sealed class UserAgentRulesTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void IsAllowed_WithNoRules_ReturnsTrue()
    {
        // Arrange
        var rules = new UserAgentRules();

        // Act
        var result = rules.IsAllowed("/admin");

        // Assert
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsAllowed_WithDisallowRule_ReturnsFalse()
    {
        // Arrange
        var rules = new UserAgentRules();
        rules.AddDisallow("/admin");

        // Act
        var result = rules.IsAllowed("/admin");

        // Assert
        result.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsAllowed_WithAllowRule_ReturnsTrue()
    {
        // Arrange
        var rules = new UserAgentRules();
        rules.AddDisallow("/admin");
        rules.AddAllow("/admin/public");

        // Act
        var result = rules.IsAllowed("/admin/public/page");

        // Assert
        result.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsAllowed_WithLongestMatchWins_ReturnsCorrectResult()
    {
        // Arrange
        var rules = new UserAgentRules();
        rules.AddDisallow("/admin");
        rules.AddAllow("/admin/public");
        rules.AddDisallow("/admin/public/secret");

        // Act & Assert
        rules.IsAllowed("/admin").Should().BeFalse();
        rules.IsAllowed("/admin/public").Should().BeTrue();
        rules.IsAllowed("/admin/public/secret").Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void AddDisallow_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var rules = new UserAgentRules();

        // Act
        var act = () => rules.AddDisallow(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void AddAllow_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var rules = new UserAgentRules();

        // Act
        var act = () => rules.AddAllow(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsAllowed_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var rules = new UserAgentRules();

        // Act
        var act = () => rules.IsAllowed(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void AddDisallow_WithEmptyPath_DoesNotAddRule()
    {
        // Arrange
        var rules = new UserAgentRules();

        // Act
        rules.AddDisallow("");
        rules.AddDisallow("   ");

        // Assert
        rules.IsAllowed("/admin").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void AddAllow_WithEmptyPath_DoesNotAddRule()
    {
        // Arrange
        var rules = new UserAgentRules();
        rules.AddDisallow("/admin");

        // Act
        rules.AddAllow("");
        rules.AddAllow("   ");

        // Assert
        rules.IsAllowed("/admin").Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void CrawlDelay_CanBeSetAndRetrieved()
    {
        // Arrange
        var rules = new UserAgentRules();

        // Act
        rules.CrawlDelay = 5.0;

        // Assert
        rules.CrawlDelay.Should().Be(5.0);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsAllowed_WithWildcardPattern_MatchesCorrectly()
    {
        // Arrange
        var rules = new UserAgentRules();
        rules.AddDisallow("/*.pdf");

        // Act & Assert
        rules.IsAllowed("/document.pdf").Should().BeFalse();
        rules.IsAllowed("/folder/file.pdf").Should().BeFalse();
        rules.IsAllowed("/document.html").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsAllowed_WithMultipleWildcards_MatchesCorrectly()
    {
        // Arrange
        var rules = new UserAgentRules();
        rules.AddDisallow("/*/admin/*");

        // Act & Assert
        rules.IsAllowed("/site/admin/users").Should().BeFalse();
        rules.IsAllowed("/admin/users").Should().BeTrue();
        rules.IsAllowed("/site/public/users").Should().BeTrue();
    }
}
