using FluentAssertions;
using Ghost.Sdk.Spiders;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Spiders;

/// <summary>
/// Integration tests for the <see cref="SitemapSpider"/> class.
/// </summary>
[Trait("Category", "Integration")]
public class SitemapSpiderIntegrationTests : ReliabilityTestBase
{
    public SitemapSpiderIntegrationTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task ParseSitemapAsync_WithRealWorldSitemap_ParsesSuccessfully()
    {
        // Arrange
        var spider = new SitemapSpider();

        // A realistic sitemap example from various sources
        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9"
                    xmlns:news="http://www.google.com/schemas/sitemap-news/0.9"
                    xmlns:xhtml="http://www.w3.org/1999/xhtml"
                    xmlns:image="http://www.google.com/schemas/sitemap-image/1.1"
                    xmlns:video="http://www.google.com/schemas/sitemap-video/1.1">
                <url>
                    <loc>https://example.com/</loc>
                    <lastmod>2024-01-15T10:30:00+00:00</lastmod>
                    <changefreq>daily</changefreq>
                    <priority>1.0</priority>
                </url>
                <url>
                    <loc>https://example.com/about</loc>
                    <lastmod>2024-01-10T14:20:00+00:00</lastmod>
                    <changefreq>monthly</changefreq>
                    <priority>0.8</priority>
                </url>
                <url>
                    <loc>https://example.com/products</loc>
                    <lastmod>2024-01-20T09:15:00+00:00</lastmod>
                    <changefreq>weekly</changefreq>
                    <priority>0.9</priority>
                </url>
                <url>
                    <loc>https://example.com/blog/post-1</loc>
                    <lastmod>2024-01-18T16:45:00+00:00</lastmod>
                    <changefreq>never</changefreq>
                    <priority>0.5</priority>
                </url>
                <url>
                    <loc>https://example.com/contact</loc>
                    <lastmod>2024-01-05T11:00:00+00:00</lastmod>
                    <changefreq>yearly</changefreq>
                    <priority>0.3</priority>
                </url>
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(5);
        urls.Should().Contain("https://example.com/");
        urls.Should().Contain("https://example.com/about");
        urls.Should().Contain("https://example.com/products");
        urls.Should().Contain("https://example.com/blog/post-1");
        urls.Should().Contain("https://example.com/contact");
    }

    [Fact]
    public async Task ParseSitemapAsync_WithSitemapIndexHierarchy_ParsesCorrectly()
    {
        // Arrange
        var spider = new SitemapSpider();

        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <sitemapindex xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <sitemap>
                    <loc>https://example.com/sitemap-products.xml</loc>
                    <lastmod>2024-01-15T10:00:00+00:00</lastmod>
                </sitemap>
                <sitemap>
                    <loc>https://example.com/sitemap-blog.xml</loc>
                    <lastmod>2024-01-20T15:30:00+00:00</lastmod>
                </sitemap>
                <sitemap>
                    <loc>https://example.com/sitemap-pages.xml</loc>
                    <lastmod>2024-01-10T08:45:00+00:00</lastmod>
                </sitemap>
            </sitemapindex>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(3);
        urls.Should().Contain("https://example.com/sitemap-products.xml");
        urls.Should().Contain("https://example.com/sitemap-blog.xml");
        urls.Should().Contain("https://example.com/sitemap-pages.xml");
    }

    [Fact]
    public async Task ParseSitemapAsync_WithComplexUrlPatterns_HandlesCorrectly()
    {
        // Arrange
        var spider = new SitemapSpider();

        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/products/category/subcategory/item-123</loc>
                    <lastmod>2024-01-15</lastmod>
                </url>
                <url>
                    <loc>https://example.com/blog/2024/01/15/my-post</loc>
                    <lastmod>2024-01-15</lastmod>
                </url>
                <url>
                    <loc>https://example.com/api/v1/documentation</loc>
                    <lastmod>2024-01-10</lastmod>
                </url>
                <url>
                    <loc>https://example.com/search?q=test&amp;filter=active</loc>
                    <lastmod>2024-01-12</lastmod>
                </url>
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(4);
        urls.Should().Contain("https://example.com/products/category/subcategory/item-123");
        urls.Should().Contain("https://example.com/blog/2024/01/15/my-post");
        urls.Should().Contain("https://example.com/api/v1/documentation");
        urls.Should().Contain("https://example.com/search?q=test&filter=active");
    }

    [Fact]
    public async Task ParseSitemapAsync_WithUnicodeUrls_HandlesCorrectly()
    {
        // Arrange
        var spider = new SitemapSpider();

        const string xmlContent = """
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
                <url>
                    <loc>https://example.com/страница</loc>
                </url>
                <url>
                    <loc>https://example.com/页面</loc>
                </url>
                <url>
                    <loc>https://example.com/ページ</loc>
                </url>
            </urlset>
            """;

        // Act
        var urls = await spider.ParseSitemapAsync(xmlContent, CancellationToken.None);

        // Assert
        urls.Should().HaveCount(3);
    }

    [Fact]
    public void SitemapOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new SitemapOptions();

        // Assert
        options.FollowSitemapIndex.Should().BeTrue();
        options.MaxDepth.Should().Be(3);
        options.LastModAfter.Should().BeNull();
    }

    [Fact]
    public void SitemapOptions_CanBeConfigured()
    {
        // Arrange
        var options = new SitemapOptions
        {
            FollowSitemapIndex = false,
            MaxDepth = 5,
            LastModAfter = TimeSpan.FromDays(7)
        };

        // Assert
        options.FollowSitemapIndex.Should().BeFalse();
        options.MaxDepth.Should().Be(5);
        options.LastModAfter.Should().Be(TimeSpan.FromDays(7));
    }
}
