using FluentAssertions;
using Ghost.Sdk.Extraction;
using Ghost.Sdk.Spiders;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Spiders;

/// <summary>
/// Integration tests for the <see cref="CrawlSpider"/> class.
/// </summary>
[Trait("Category", "Integration")]
public class CrawlSpiderIntegrationTests : ReliabilityTestBase
{
    public CrawlSpiderIntegrationTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task CrawlSpider_WithRealLinkExtractor_ExtractsLinksAndItems()
    {
        // Arrange
        var linkExtractor = new HtmlAgilityLinkExtractor();
        var spider = new CrawlSpider(linkExtractor);

        // Add rule to parse product pages
        spider.AddRule(
            name: "ProductPages",
            followCondition: url => url.Contains("/products/"),
            parseAction: response =>
            {
                var item = new Item
                {
                    SourceUrl = response.Url,
                    Metadata =
                    {
                        ["type"] = "product",
                        ["title"] = "Sample Product"
                    }
                };
                return new[] { item };
            }
        );

        // Add rule to follow category pages
        spider.AddRule(
            name: "CategoryPages",
            followCondition: url => url.Contains("/category/"),
            parseAction: _ => Enumerable.Empty<Item>()
        );

        // HTML content with links
        const string htmlContent = """
            <html>
            <head><title>Test Page</title></head>
            <body>
                <a href="https://example.com/products/123">Product 123</a>
                <a href="https://example.com/products/456">Product 456</a>
                <a href="https://example.com/category/electronics">Electronics</a>
                <a href="https://example.com/about">About Us</a>
            </body>
            </html>
            """;

        var response = new Response
        {
            Url = "https://example.com/category/all",
            Body = htmlContent,
            IsSuccess = true
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        var scheduledRequests = spider.GetScheduledRequests().ToList();
        scheduledRequests.Should().HaveCount(3); // 2 products + 1 category
        scheduledRequests.Should().Contain(r => r.Url == "https://example.com/products/123");
        scheduledRequests.Should().Contain(r => r.Url == "https://example.com/products/456");
        scheduledRequests.Should().Contain(r => r.Url == "https://example.com/category/electronics");
        scheduledRequests.Should().NotContain(r => r.Url == "https://example.com/about");
    }

    [Fact]
    public async Task CrawlSpider_WithRegexLinkExtractor_FollowsMatchingPatterns()
    {
        // Arrange
        var linkExtractor = new RegexLinkExtractor();
        var spider = new CrawlSpider(linkExtractor);

        // Add rule to parse all pages
        spider.AddRule(
            name: "AllPages",
            followCondition: url => url.Contains("example.com"),
            parseAction: response =>
            {
                var item = new Item
                {
                    SourceUrl = response.Url,
                    Metadata = { ["extracted"] = true }
                };
                return new[] { item };
            }
        );

        // HTML with various link formats
        const string htmlContent = """
            <html>
            <body>
                <a href="https://example.com/page1">Page 1</a>
                <a href="https://example.com/page2">Page 2</a>
                <a href="https://external.com/page3">External</a>
            </body>
            </html>
            """;

        var response = new Response
        {
            Url = "https://example.com/home",
            Body = htmlContent,
            IsSuccess = true
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        var scheduledRequests = spider.GetScheduledRequests().ToList();
        scheduledRequests.Should().Contain(r => r.Url.Contains("example.com"));
        scheduledRequests.Should().NotContain(r => r.Url.Contains("external.com"));
    }

    [Fact]
    public async Task CrawlSpider_WithMultipleRules_PrioritizesBasedOnOrder()
    {
        // Arrange
        var linkExtractor = new HtmlAgilityLinkExtractor();
        var spider = new CrawlSpider(linkExtractor);

        var rule1ItemsExtracted = 0;
        var rule2ItemsExtracted = 0;

        // First rule - broad match
        spider.AddRule(
            name: "AllPages",
            followCondition: url => url.Contains("example.com"),
            parseAction: response =>
            {
                rule1ItemsExtracted++;
                return new[] { new Item { Metadata = { ["rule"] = "all" } } };
            }
        );

        // Second rule - specific match
        spider.AddRule(
            name: "ProductPages",
            followCondition: url => url.Contains("/products/"),
            parseAction: response =>
            {
                rule2ItemsExtracted++;
                return new[] { new Item { Metadata = { ["rule"] = "product" } } };
            }
        );

        var response = new Response
        {
            Url = "https://example.com/products/123",
            Body = "<html><body>Product Page</body></html>",
            IsSuccess = true
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        rule1ItemsExtracted.Should().Be(1); // Broad rule matches
        rule2ItemsExtracted.Should().Be(1); // Specific rule also matches
        spider.GetExtractedItems().Should().HaveCount(2); // Both rules extract items
    }

    [Fact]
    public async Task CrawlSpider_WithRelativeLinks_ResolvesToAbsoluteUrls()
    {
        // Arrange
        var linkExtractor = new HtmlAgilityLinkExtractor();
        var spider = new CrawlSpider(linkExtractor);

        spider.AddRule(
            name: "AllPages",
            followCondition: url => url.Contains("example.com"),
            parseAction: _ => Enumerable.Empty<Item>()
        );

        const string htmlContent = """
            <html>
            <body>
                <a href="/products/123">Product 123</a>
                <a href="/category/electronics">Electronics</a>
                <a href="../about">About</a>
            </body>
            </html>
            """;

        var response = new Response
        {
            Url = "https://example.com/home",
            Body = htmlContent,
            IsSuccess = true
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        var scheduledRequests = spider.GetScheduledRequests().ToList();
        scheduledRequests.Should().OnlyContain(r => r.Url.StartsWith("https://"));
        scheduledRequests.Should().Contain(r => r.Url == "https://example.com/products/123");
        scheduledRequests.Should().Contain(r => r.Url == "https://example.com/category/electronics");
        scheduledRequests.Should().Contain(r => r.Url == "https://example.com/about");
    }

    [Fact]
    public async Task CrawlSpider_WithComplexCrawlingScenario_HandlesMultiplePagesAndRules()
    {
        // Arrange
        var linkExtractor = new HtmlAgilityLinkExtractor();
        var spider = new CrawlSpider(linkExtractor);

        List<Item> productItems = [];
        List<Item> categoryItems = [];

        // Rule for products - extract items
        spider.AddRule(
            name: "Products",
            followCondition: url => url.Contains("/product/"),
            parseAction: response =>
            {
                var item = new Item
                {
                    SourceUrl = response.Url,
                    Metadata = { ["type"] = "product" }
                };
                productItems.Add(item);
                return new[] { item };
            }
        );

        // Rule for categories - no extraction, just follow
        spider.AddRule(
            name: "Categories",
            followCondition: url => url.Contains("/category/"),
            parseAction: response =>
            {
                var item = new Item
                {
                    SourceUrl = response.Url,
                    Metadata = { ["type"] = "category" }
                };
                categoryItems.Add(item);
                return new[] { item };
            }
        );

        // Simulate homepage
        var homepageHtml = """
            <html>
            <body>
                <a href="/category/electronics">Electronics</a>
                <a href="/category/books">Books</a>
            </body>
            </html>
            """;

        await spider.ParseAsync(new Response
        {
            Url = "https://example.com/",
            Body = homepageHtml,
            IsSuccess = true
        }, CancellationToken.None);

        // Simulate category page
        var categoryHtml = """
            <html>
            <body>
                <a href="/product/laptop">Laptop</a>
                <a href="/product/phone">Phone</a>
            </body>
            </html>
            """;

        await spider.ParseAsync(new Response
        {
            Url = "https://example.com/category/electronics",
            Body = categoryHtml,
            IsSuccess = true
        }, CancellationToken.None);

        // Simulate product page
        var productHtml = """
            <html>
            <body>
                <h1>Laptop</h1>
                <a href="/product/mouse">Related: Mouse</a>
            </body>
            </html>
            """;

        await spider.ParseAsync(new Response
        {
            Url = "https://example.com/product/laptop",
            Body = productHtml,
            IsSuccess = true
        }, CancellationToken.None);

        // Assert
        productItems.Should().HaveCount(1);
        productItems[0].SourceUrl.Should().Be("https://example.com/product/laptop");

        categoryItems.Should().HaveCount(1);
        categoryItems[0].SourceUrl.Should().Be("https://example.com/category/electronics");

        var allItems = spider.GetExtractedItems().ToList();
        allItems.Should().HaveCount(2); // 1 category + 1 product

        var allRequests = spider.GetScheduledRequests().ToList();
        allRequests.Should().Contain(r => r.Url == "https://example.com/category/electronics");
        allRequests.Should().Contain(r => r.Url == "https://example.com/category/books");
        allRequests.Should().Contain(r => r.Url == "https://example.com/product/laptop");
        allRequests.Should().Contain(r => r.Url == "https://example.com/product/phone");
        allRequests.Should().Contain(r => r.Url == "https://example.com/product/mouse");
    }
}
