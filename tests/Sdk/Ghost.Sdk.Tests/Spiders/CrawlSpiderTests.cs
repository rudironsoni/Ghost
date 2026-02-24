using FluentAssertions;
using Ghost.Sdk.Extraction;
using Ghost.Sdk.Spiders;
using NSubstitute;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Spiders;

/// <summary>
/// Unit tests for the <see cref="CrawlSpider"/> class.
/// </summary>
[Trait("Category", "Unit")]
public class CrawlSpiderTests : ReliabilityTestBase
{
    private readonly ILinkExtractor _mockLinkExtractor;

    public CrawlSpiderTests(ITestOutputHelper output) : base(output)
    {
        _mockLinkExtractor = Substitute.For<ILinkExtractor>();
    }

    [Fact]
    public void Constructor_WithNullLinkExtractor_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new CrawlSpider(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("linkExtractor");
    }

    [Fact]
    public void Constructor_WithValidLinkExtractor_InitializesEmptyRulesList()
    {
        // Act
        var spider = new CrawlSpider(_mockLinkExtractor);

        // Assert
        spider.Rules.Should().NotBeNull();
        spider.Rules.Should().BeEmpty();
    }

    [Fact]
    public void AddRule_WithValidParameters_AddsRuleToList()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);
        var followCondition = new Func<string, bool>(url => url.Contains("/products/"));
        var parseAction = new Func<Response, IEnumerable<Item>>(_ => Enumerable.Empty<Item>());

        // Act
        spider.AddRule("ProductPages", followCondition, parseAction);

        // Assert
        spider.Rules.Should().HaveCount(1);
        spider.Rules[0].Name.Should().Be("ProductPages");
        spider.Rules[0].FollowCondition.Should().BeSameAs(followCondition);
        spider.Rules[0].ParseAction.Should().BeSameAs(parseAction);
    }

    [Fact]
    public void AddRule_WithNullName_ThrowsArgumentException()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);
        var followCondition = new Func<string, bool>(url => true);
        var parseAction = new Func<Response, IEnumerable<Item>>(_ => Enumerable.Empty<Item>());

        // Act
        var act = () => spider.AddRule(null!, followCondition, parseAction);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRule_WithEmptyName_ThrowsArgumentException()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);
        var followCondition = new Func<string, bool>(url => true);
        var parseAction = new Func<Response, IEnumerable<Item>>(_ => Enumerable.Empty<Item>());

        // Act
        var act = () => spider.AddRule(string.Empty, followCondition, parseAction);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void AddRule_WithNullFollowCondition_ThrowsArgumentNullException()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);
        var parseAction = new Func<Response, IEnumerable<Item>>(_ => Enumerable.Empty<Item>());

        // Act
        var act = () => spider.AddRule("TestRule", null!, parseAction);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("followCondition");
    }

    [Fact]
    public void AddRule_WithNullParseAction_ThrowsArgumentNullException()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);
        var followCondition = new Func<string, bool>(url => true);

        // Act
        var act = () => spider.AddRule("TestRule", followCondition, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("parseAction");
    }

    [Fact]
    public void AddRule_WithMultipleRules_MaintainsOrderAndAllRules()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        // Act
        spider.AddRule("Rule1", url => url.Contains("/a/"), _ => Enumerable.Empty<Item>());
        spider.AddRule("Rule2", url => url.Contains("/b/"), _ => Enumerable.Empty<Item>());
        spider.AddRule("Rule3", url => url.Contains("/c/"), _ => Enumerable.Empty<Item>());

        // Assert
        spider.Rules.Should().HaveCount(3);
        spider.Rules[0].Name.Should().Be("Rule1");
        spider.Rules[1].Name.Should().Be("Rule2");
        spider.Rules[2].Name.Should().Be("Rule3");
    }

    [Fact]
    public async Task ParseAsync_WithNullResponse_ThrowsArgumentNullException()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        // Act
        var act = async () => await spider.ParseAsync(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("response");
    }

    [Fact]
    public async Task ParseAsync_WithMatchingRule_ExecutesParseAction()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);
        var itemExtracted = false;
        var item = new Item { SourceUrl = "https://example.com/product/123" };

        spider.AddRule(
            "ProductPages",
            url => url.Contains("/product/"),
            response =>
            {
                itemExtracted = true;
                return new[] { item };
            }
        );

        var response = new Response
        {
            Url = "https://example.com/product/123",
            Body = "<html><body>Test</body></html>",
            IsSuccess = true
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        itemExtracted.Should().BeTrue();
        spider.GetExtractedItems().Should().ContainSingle()
            .Which.Should().BeSameAs(item);
    }

    [Fact]
    public async Task ParseAsync_WithNonMatchingRule_DoesNotExecuteParseAction()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);
        var parseActionCalled = false;

        spider.AddRule(
            "ProductPages",
            url => url.Contains("/product/"),
            response =>
            {
                parseActionCalled = true;
                return Enumerable.Empty<Item>();
            }
        );

        var response = new Response
        {
            Url = "https://example.com/category/electronics",
            Body = "<html><body>Test</body></html>",
            IsSuccess = true
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        parseActionCalled.Should().BeFalse();
        spider.GetExtractedItems().Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithMultipleMatchingRules_ExecutesAllMatchingParseActions()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);
        var rule1Called = false;
        var rule2Called = false;
        var rule3Called = false;

        var item1 = new Item { Metadata = { ["source"] = "rule1" } };
        var item2 = new Item { Metadata = { ["source"] = "rule2" } };

        spider.AddRule(
            "AllPages",
            url => url.Contains("example.com"),
            response =>
            {
                rule1Called = true;
                return new[] { item1 };
            }
        );

        spider.AddRule(
            "ProductPages",
            url => url.Contains("/product/"),
            response =>
            {
                rule2Called = true;
                return new[] { item2 };
            }
        );

        spider.AddRule(
            "CategoryPages",
            url => url.Contains("/category/"),
            response =>
            {
                rule3Called = true;
                return Enumerable.Empty<Item>();
            }
        );

        var response = new Response
        {
            Url = "https://example.com/product/123",
            Body = "<html><body>Test</body></html>",
            IsSuccess = true
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        rule1Called.Should().BeTrue();
        rule2Called.Should().BeTrue();
        rule3Called.Should().BeFalse(); // Should not match /category/
        spider.GetExtractedItems().Should().HaveCount(2);
        spider.GetExtractedItems().Should().Contain(item1);
        spider.GetExtractedItems().Should().Contain(item2);
    }

    [Fact]
    public async Task ParseAsync_WithSuccessfulResponse_ExtractsAndFollowsLinks()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        spider.AddRule(
            "ProductPages",
            url => url.Contains("/product/"),
            _ => Enumerable.Empty<Item>()
        );

        var response = new Response
        {
            Url = "https://example.com/category/electronics",
            Body = "<html><body><a href='/product/1'>Product 1</a></body></html>",
            IsSuccess = true
        };

        var extractedLinks = new List<string>
        {
            "https://example.com/product/1",
            "https://example.com/product/2",
            "https://example.com/about"
        };

        _mockLinkExtractor.ExtractLinks(response.Body, response.Url)
            .Returns(extractedLinks);

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        var scheduledRequests = spider.GetScheduledRequests().ToList();
        scheduledRequests.Should().HaveCount(2); // Only product URLs match the rule
        scheduledRequests.Should().Contain(r => r.Url == "https://example.com/product/1");
        scheduledRequests.Should().Contain(r => r.Url == "https://example.com/product/2");
        scheduledRequests.Should().NotContain(r => r.Url == "https://example.com/about");
    }

    [Fact]
    public async Task ParseAsync_WithFailedResponse_DoesNotExtractLinks()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        spider.AddRule(
            "ProductPages",
            url => url.Contains("/product/"),
            _ => Enumerable.Empty<Item>()
        );

        var response = new Response
        {
            Url = "https://example.com/product/123",
            Body = string.Empty,
            IsSuccess = false,
            StatusCode = 404
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        _mockLinkExtractor.DidNotReceive().ExtractLinks(Arg.Any<string>(), Arg.Any<string>());
        spider.GetScheduledRequests().Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithEmptyBody_DoesNotExtractLinks()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        spider.AddRule(
            "ProductPages",
            url => url.Contains("/product/"),
            _ => Enumerable.Empty<Item>()
        );

        var response = new Response
        {
            Url = "https://example.com/product/123",
            Body = string.Empty,
            IsSuccess = true
        };

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        _mockLinkExtractor.DidNotReceive().ExtractLinks(Arg.Any<string>(), Arg.Any<string>());
        spider.GetScheduledRequests().Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_WithNoMatchingRulesForLinks_DoesNotScheduleRequests()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        spider.AddRule(
            "ProductPages",
            url => url.Contains("/product/"),
            _ => Enumerable.Empty<Item>()
        );

        var response = new Response
        {
            Url = "https://example.com/about",
            Body = "<html><body><a href='/contact'>Contact</a></body></html>",
            IsSuccess = true
        };

        var extractedLinks = new List<string>
        {
            "https://example.com/about",
            "https://example.com/contact"
        };

        _mockLinkExtractor.ExtractLinks(response.Body, response.Url)
            .Returns(extractedLinks);

        // Act
        await spider.ParseAsync(response, CancellationToken.None);

        // Assert
        spider.GetScheduledRequests().Should().BeEmpty();
    }

    [Fact]
    public async Task ParseAsync_IntegrationScenario_ExtractsItemsAndSchedulesRequests()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        // Rule 1: Extract items from product pages
        spider.AddRule(
            "ProductPages",
            url => url.Contains("/product/"),
            response => new[]
            {
                new Item
                {
                    SourceUrl = response.Url,
                    Metadata = { ["type"] = "product" }
                }
            }
        );

        // Rule 2: Follow category pages but don't extract
        spider.AddRule(
            "CategoryPages",
            url => url.Contains("/category/"),
            _ => Enumerable.Empty<Item>()
        );

        // First response: category page with links to products
        var categoryResponse = new Response
        {
            Url = "https://example.com/category/electronics",
            Body = "<html><body>Category page</body></html>",
            IsSuccess = true
        };

        var categoryLinks = new List<string>
        {
            "https://example.com/product/1",
            "https://example.com/product/2",
            "https://example.com/category/computers"
        };

        _mockLinkExtractor.ExtractLinks(categoryResponse.Body, categoryResponse.Url)
            .Returns(categoryLinks);

        // Act - Parse category page
        await spider.ParseAsync(categoryResponse, CancellationToken.None);

        // Assert - Category page should schedule product and category links
        spider.GetScheduledRequests().Should().HaveCount(3);
        spider.GetExtractedItems().Should().BeEmpty(); // No items from category page

        // Second response: product page
        var productResponse = new Response
        {
            Url = "https://example.com/product/1",
            Body = "<html><body>Product page</body></html>",
            IsSuccess = true
        };

        var productLinks = new List<string>
        {
            "https://example.com/product/2",
            "https://example.com/category/electronics"
        };

        _mockLinkExtractor.ExtractLinks(productResponse.Body, productResponse.Url)
            .Returns(productLinks);

        // Act - Parse product page
        await spider.ParseAsync(productResponse, CancellationToken.None);

        // Assert - Product page should extract item and schedule links
        spider.GetExtractedItems().Should().HaveCount(1);
        spider.GetExtractedItems().First().SourceUrl.Should().Be("https://example.com/product/1");
        spider.GetExtractedItems().First().Metadata["type"].Should().Be("product");
        spider.GetScheduledRequests().Should().HaveCount(5); // 3 from category + 2 from product
    }

    [Fact]
    public void GetScheduledRequests_ReturnsReadOnlyCollection()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        // Act
        var requests = spider.GetScheduledRequests();

        // Assert
        requests.Should().NotBeNull();
        requests.Should().BeAssignableTo<IEnumerable<Request>>();
    }

    [Fact]
    public void GetExtractedItems_ReturnsReadOnlyCollection()
    {
        // Arrange
        var spider = new CrawlSpider(_mockLinkExtractor);

        // Act
        var items = spider.GetExtractedItems();

        // Assert
        items.Should().NotBeNull();
        items.Should().BeAssignableTo<IEnumerable<Item>>();
    }
}
