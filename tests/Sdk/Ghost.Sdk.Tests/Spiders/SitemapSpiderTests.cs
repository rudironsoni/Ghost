using FluentAssertions;
using Ghost.Sdk.Spiders;
using Xunit;

namespace Ghost.Sdk.Tests.Spiders;

/// <summary>
/// Unit tests for the <see cref="SitemapSpider"/> class.
/// </summary>
[Trait("Category", "Unit")]
public class SitemapSpiderTests
{
    [Fact]
    public void Constructor_WithNoArguments_SetsDefaultProperties()
    {
        // Act
        var spider = new SitemapSpider();

        // Assert
        spider.Name.Should().Be("SitemapSpider");
        spider.SitemapUrl.Should().BeEmpty();
        spider.Options.Should().NotBeNull();
        spider.Options.FollowSitemapIndex.Should().BeTrue();
        spider.Options.MaxDepth.Should().Be(3);
    }

    [Fact]
    public void Constructor_WithSitemapUrl_SetsSitemapUrl()
    {
        // Arrange
        const string url = "https://example.com/sitemap.xml";

        // Act
        var spider = new SitemapSpider(url);

        // Assert
        spider.SitemapUrl.Should().Be(url);
    }

    [Fact]
    public void Constructor_WithNullSitemapUrl_ThrowsArgumentException()
    {
        // Act
        var act = () => new SitemapSpider(null!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_WithEmptySitemapUrl_ThrowsArgumentException()
    {
        // Act
        var act = () => new SitemapSpider(string.Empty);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public async Task ParseSitemapAsync_WithUrlSet_ExtractsUrls()
    {
        // Arrange
        var spider = new SitemapSpider();
        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/page1</loc>
                    <lastmod>2024-01-01</lastmod>
                </url>
                <url>
                    <loc>https://example.com/page2</loc>
                    <lastmod>2024-01-02</lastmod>
                </url>
                <url>
                    <loc>https://example.com/page3</loc>
                </url>
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(3);
        urls.Should().Contain("https://example.com/page1");
        urls.Should().Contain("https://example.com/page2");
        urls.Should().Contain("https://example.com/page3");
    }

    [Fact]
    public async Task ParseSitemapAsync_WithSitemapIndex_ExtractsSitemapUrls()
    {
        // Arrange
        var spider = new SitemapSpider();
        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <sitemap>
                    <loc>https://example.com/sitemap1.xml</loc>
                </sitemap>
                <sitemap>
                    <loc>https://example.com/sitemap2.xml</loc>
                </sitemap>
            </sitemapindex>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(2);
        urls.Should().Contain("https://example.com/sitemap1.xml");
        urls.Should().Contain("https://example.com/sitemap2.xml");
    }

    [Fact]
    public async Task ParseSitemapAsync_WithMixedContent_ExtractsAllUrls()
    {
        // Arrange
        var spider = new SitemapSpider();
        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/page1</loc>
                </url>
                <url>
                    <loc>https://example.com/page2</loc>
                </url>
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseSitemapAsync_WithEmptyXml_ReturnsEmptyList()
    {
        // Arrange
        var spider = new SitemapSpider();
        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseSitemapAsync_WithInvalidXml_ReturnsEmptyList()
    {
        // Arrange
        var spider = new SitemapSpider();
        const string xmlContent = "This is not valid XML";

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseSitemapAsync_WithNullXmlContent_ThrowsArgumentException()
    {
        // Arrange
        var spider = new SitemapSpider();

        // Act
        var act = async () => await spider.ParseSitemapAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ParseSitemapAsync_WithEmptyXmlContent_ThrowsArgumentException()
    {
        // Arrange
        var spider = new SitemapSpider();

        // Act
        var act = async () => await spider.ParseSitemapAsync(string.Empty, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ParseSitemapAsync_WithLastModAfterFilter_FiltersOldUrls()
    {
        // Arrange
        var spider = new SitemapSpider
        {
            Options = new SitemapOptions
            {
                LastModAfter = TimeSpan.FromDays(30) // Only URLs modified in last 30 days
            }
        };

        var recentDate = DateTime.UtcNow.AddDays(-10).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        var oldDate = DateTime.UtcNow.AddDays(-60).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        var xmlContent = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/recent-page</loc>
                    <lastmod>{recentDate}</lastmod>
                </url>
                <url>
                    <loc>https://example.com/old-page</loc>
                    <lastmod>{oldDate}</lastmod>
                </url>
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(1);
        urls.Should().Contain("https://example.com/recent-page");
        urls.Should().NotContain("https://example.com/old-page");
    }

    [Fact]
    public async Task ParseSitemapAsync_WithMissingLocElements_IgnoresEntries()
    {
        // Arrange
        var spider = new SitemapSpider();
        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/page1</loc>
                </url>
                <url>
                    <!-- Missing loc element -->
                    <lastmod>2024-01-01</lastmod>
                </url>
                <url>
                    <loc>https://example.com/page2</loc>
                </url>
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(2);
        urls.Should().Contain("https://example.com/page1");
        urls.Should().Contain("https://example.com/page2");
    }

    [Fact]
    public async Task ParseSitemapAsync_WithEmptyLocElements_IgnoresEntries()
    {
        // Arrange
        var spider = new SitemapSpider();
        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/page1</loc>
                </url>
                <url>
                    <loc></loc>
                </url>
                <url>
                    <loc>https://example.com/page2</loc>
                </url>
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(2);
        urls.Should().Contain("https://example.com/page1");
        urls.Should().Contain("https://example.com/page2");
    }

    [Fact]
    public async Task StartAsync_WithoutSitemapUrl_ThrowsArgumentException()
    {
        // Arrange
        var spider = new SitemapSpider();

        // Act
        var act = async () => await spider.StartAsync(CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
