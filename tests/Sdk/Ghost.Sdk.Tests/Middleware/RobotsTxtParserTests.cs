using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class RobotsTxtParserTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithEmptyContent_ReturnsEmptyRobotsTxt()
    {
        // Arrange
        var content = string.Empty;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.Should().NotBeNull();
        result.CanFetch("/", "TestBot").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithSimpleDisallow_ParsesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow: /admin
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/", "TestBot").Should().BeTrue();
        result.CanFetch("/admin", "TestBot").Should().BeFalse();
        result.CanFetch("/admin/users", "TestBot").Should().BeFalse();
        result.CanFetch("/public", "TestBot").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithMultipleUserAgents_ParsesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: Googlebot
            Disallow: /private

            User-agent: *
            Disallow: /admin
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/private", "Googlebot").Should().BeFalse();
        result.CanFetch("/admin", "Googlebot").Should().BeTrue();
        result.CanFetch("/private", "OtherBot").Should().BeTrue();
        result.CanFetch("/admin", "OtherBot").Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithAllowAndDisallow_ParsesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow: /admin
            Allow: /admin/public
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/admin", "TestBot").Should().BeFalse();
        result.CanFetch("/admin/private", "TestBot").Should().BeFalse();
        result.CanFetch("/admin/public", "TestBot").Should().BeTrue();
        result.CanFetch("/admin/public/page", "TestBot").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithWildcardPattern_ParsesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow: /*.pdf$
            Disallow: /temp*
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/document.pdf", "TestBot").Should().BeFalse();
        result.CanFetch("/temp", "TestBot").Should().BeFalse();
        result.CanFetch("/temporary", "TestBot").Should().BeFalse();
        result.CanFetch("/document.html", "TestBot").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithCrawlDelay_ParsesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: *
            Crawl-delay: 5
            Disallow: /admin
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.GetCrawlDelay("TestBot").Should().Be(5);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithUserAgentSpecificCrawlDelay_ParsesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: SlowBot
            Crawl-delay: 10
            Disallow:

            User-agent: *
            Crawl-delay: 1
            Disallow:
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.GetCrawlDelay("SlowBot").Should().Be(10);
        result.GetCrawlDelay("OtherBot").Should().Be(1);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithSitemap_ParsesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow: /admin

            Sitemap: https://example.com/sitemap.xml
            Sitemap: https://example.com/sitemap2.xml
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.Sitemaps.Should().HaveCount(2);
        result.Sitemaps.Should().Contain("https://example.com/sitemap.xml");
        result.Sitemaps.Should().Contain("https://example.com/sitemap2.xml");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithComments_IgnoresComments()
    {
        // Arrange
        var content = """
            # This is a comment
            User-agent: * # Inline comment
            Disallow: /admin # Admin area
            # Another comment
            Allow: /public
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/admin", "TestBot").Should().BeFalse();
        result.CanFetch("/public", "TestBot").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithEmptyDisallow_AllowsAll()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow:
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/", "TestBot").Should().BeTrue();
        result.CanFetch("/admin", "TestBot").Should().BeTrue();
        result.CanFetch("/anything", "TestBot").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithDisallowAll_BlocksEverything()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow: /
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/", "TestBot").Should().BeFalse();
        result.CanFetch("/admin", "TestBot").Should().BeFalse();
        result.CanFetch("/anything", "TestBot").Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithCaseInsensitiveDirectives_ParsesCorrectly()
    {
        // Arrange
        var content = """
            user-agent: *
            DISALLOW: /admin
            Allow: /public
            crawl-delay: 5
            SITEMAP: https://example.com/sitemap.xml
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/admin", "TestBot").Should().BeFalse();
        result.CanFetch("/public", "TestBot").Should().BeTrue();
        result.GetCrawlDelay("TestBot").Should().Be(5);
        result.Sitemaps.Should().Contain("https://example.com/sitemap.xml");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithUnknownDirectives_IgnoresThem()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow: /admin
            Unknown-Directive: value
            Custom-Field: data
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/admin", "TestBot").Should().BeFalse();
        result.CanFetch("/public", "TestBot").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithNullContent_ThrowsArgumentNullException()
    {
        // Act
        var act = () => RobotsTxtParser.Parse(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithMalformedLines_IgnoresThem()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow /admin
            Disallow: /private
            InvalidLine
            Allow: /public
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/private", "TestBot").Should().BeFalse();
        result.CanFetch("/public", "TestBot").Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithPartialUserAgentMatch_MatchesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: Googlebot
            Disallow: /private

            User-agent: *
            Disallow: /admin
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        result.CanFetch("/private", "Googlebot/2.1").Should().BeFalse();
        result.CanFetch("/private", "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)").Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Parse_WithMostSpecificRuleWins_AppliesCorrectly()
    {
        // Arrange
        var content = """
            User-agent: *
            Disallow: /admin
            Allow: /admin/public
            Allow: /admin/public/api
            """;

        // Act
        var result = RobotsTxtParser.Parse(content);

        // Assert
        // Most specific (longest) rule wins
        result.CanFetch("/admin", "TestBot").Should().BeFalse();
        result.CanFetch("/admin/public", "TestBot").Should().BeTrue();
        result.CanFetch("/admin/public/api", "TestBot").Should().BeTrue();
    }
}
