using System.Text.RegularExpressions;
using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Engine;
using Ghost.Sdk.Spider.Extraction;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Xunit;
using SpiderExecutionContext = Ghost.Sdk.Spider.Engine.ExecutionContext;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Engine;

/// <summary>
/// Unit tests for the CrawlSpider and Rule implementations.
/// </summary>
public class CrawlSpiderTests : ReliabilityTestBase
{
    public CrawlSpiderTests(ITestOutputHelper output) : base(output) { }
    private readonly TestLinkExtractor _mockLinkExtractor;
    private static readonly string[] TestLinks = ["https://example.com/link1", "https://example.com/link2"];

    public CrawlSpiderTests()
    {
        _mockLinkExtractor = new TestLinkExtractor();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithNullLinkExtractor_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new TestCrawlSpider(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("linkExtractor");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRule_WithValidRule_AddsToRulesList()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var rule = new Rule
        {
            Pattern = new Regex(@"/test"),
            Follow = true
        };

        // Act
        spider.AddRule(rule);

        // Assert
        spider.Rules.Should().HaveCount(1);
        spider.Rules[0].Should().BeSameAs(rule);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRule_WithNullRule_ThrowsArgumentNullException()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);

        // Act & Assert
        var act = () => spider.AddRule(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("rule");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AddRule_WithMultipleRules_PreservesOrder()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var rule1 = new Rule { Pattern = new Regex(@"/first") };
        var rule2 = new Rule { Pattern = new Regex(@"/second") };
        var rule3 = new Rule { Pattern = new Regex(@"/third") };

        // Act
        spider.AddRule(rule1);
        spider.AddRule(rule2);
        spider.AddRule(rule3);

        // Assert
        spider.Rules.Should().HaveCount(3);
        spider.Rules[0].Should().BeSameAs(rule1);
        spider.Rules[1].Should().BeSameAs(rule2);
        spider.Rules[2].Should().BeSameAs(rule3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithMatchingRule_InvokesCallback()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var callbackInvoked = false;
        var rule = new Rule
        {
            Pattern = new Regex(@"example\.com"),
            Callback = _ =>
            {
                callbackInvoked = true;
                return Task.FromResult(Enumerable.Empty<object>());
            },
            Follow = false
        };
        spider.AddRule(rule);

        var response = TestData.CreateResponse("https://example.com/page");
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithNonMatchingRule_DoesNotInvokeCallback()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var callbackInvoked = false;
        var rule = new Rule
        {
            Pattern = new Regex(@"test\.com"),
            Callback = _ =>
            {
                callbackInvoked = true;
                return Task.FromResult(Enumerable.Empty<object>());
            },
            Follow = false
        };
        spider.AddRule(rule);

        var response = TestData.CreateResponse("https://example.com/page");
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        callbackInvoked.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithMultipleMatchingRules_InvokesAllCallbacks()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var callback1Invoked = false;
        var callback2Invoked = false;

        var rule1 = new Rule
        {
            Pattern = new Regex(@"example\.com"),
            Callback = _ =>
            {
                callback1Invoked = true;
                return Task.FromResult(Enumerable.Empty<object>());
            },
            Follow = false
        };

        var rule2 = new Rule
        {
            Pattern = new Regex(@"/page"),
            Callback = _ =>
            {
                callback2Invoked = true;
                return Task.FromResult(Enumerable.Empty<object>());
            },
            Follow = false
        };

        spider.AddRule(rule1);
        spider.AddRule(rule2);

        var response = TestData.CreateResponse("https://example.com/page");
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        callback1Invoked.Should().BeTrue();
        callback2Invoked.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithFollowTrue_ExtractsLinks()
    {
        // Arrange
        var html = @"
            <html>
                <body>
                    <a href=""https://example.com/link1"">Link 1</a>
                    <a href=""https://example.com/link2"">Link 2</a>
                </body>
            </html>";

        _mockLinkExtractor.SetLinks(TestLinks);

        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var rule = new Rule
        {
            Pattern = new Regex(@"example\.com"),
            Callback = _ => Task.FromResult(Enumerable.Empty<object>()),
            Follow = true
        };
        spider.AddRule(rule);

        var response = TestData.CreateResponse("https://example.com/page", html, contentType: ContentType.Html);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        var pendingRequests = spider.GetPendingRequests(context).ToList();
        pendingRequests.Should().HaveCount(2);
        pendingRequests.Select(r => r.Url).Should().Contain("https://example.com/link1");
        pendingRequests.Select(r => r.Url).Should().Contain("https://example.com/link2");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithFollowFalse_DoesNotExtractLinks()
    {
        // Arrange
        var html = "<html><body><a href=\"https://example.com/link\">Link</a></body></html>";
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var rule = new Rule
        {
            Pattern = new Regex(@"example\.com"),
            Callback = _ => Task.FromResult(Enumerable.Empty<object>()),
            Follow = false
        };
        spider.AddRule(rule);

        var response = TestData.CreateResponse("https://example.com/page", html, contentType: ContentType.Html);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        _mockLinkExtractor.DidNotExtractLinks.Should().BeTrue();
        var pendingRequests = spider.GetPendingRequests(context).ToList();
        pendingRequests.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithExtractedItems_IncrementsCounter()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var testItem = new TestEntity { Id = "123", Title = "Test Item" };

        var rule = new Rule
        {
            Pattern = new Regex(@"example\.com"),
            Callback = _ => Task.FromResult<IEnumerable<object>>(new[] { testItem }),
            Follow = false
        };
        spider.AddRule(rule);

        var response = TestData.CreateResponse("https://example.com/page");
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        context.ItemsExtracted.Should().Be(1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithMultipleItems_IncrementsCounterCorrectly()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var items = new object[]
        {
            new TestEntity { Id = "1", Title = "Item 1" },
            new TestEntity { Id = "2", Title = "Item 2" },
            new TestEntity { Id = "3", Title = "Item 3" }
        };

        var rule = new Rule
        {
            Pattern = new Regex(@"example\.com"),
            Callback = _ => Task.FromResult<IEnumerable<object>>(items),
            Follow = false
        };
        spider.AddRule(rule);

        var response = TestData.CreateResponse("https://example.com/page");
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        context.ItemsExtracted.Should().Be(3);
        var extractedItems = spider.GetExtractedItems(context).ToList();
        extractedItems.Should().HaveCount(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithNullResponse_ThrowsArgumentNullException()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act & Assert
        await spider.Invoking(s => s.ProcessResponseAsync(null!, context))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("response");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var response = TestData.CreateResponse();

        // Act & Assert
        await spider.Invoking(s => s.ProcessResponseAsync(response, null!))
            .Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_WithNonHtmlContent_DoesNotExtractLinks()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var rule = new Rule
        {
            Pattern = new Regex(@"example\.com"),
            Callback = _ => Task.FromResult(Enumerable.Empty<object>()),
            Follow = true
        };
        spider.AddRule(rule);

        var response = TestData.CreateResponse("https://example.com/api", "{\"data\": \"json\"}", contentType: ContentType.Json);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        _mockLinkExtractor.DidNotExtractLinks.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessResponseAsync_FiltersLinksWithShouldFollowUrl()
    {
        // Arrange
        var links = new[]
        {
            "https://example.com/allowed",
            "https://blocked.com/page",
            "https://example.com/allowed2"
        };
        _mockLinkExtractor.SetLinks(links);

        var spider = new TestCrawlSpider(_mockLinkExtractor)
        {
            AllowedDomains = new List<string> { "example.com" }
        };

        var rule = new Rule
        {
            Pattern = new Regex(@"example\.com"),
            Callback = _ => Task.FromResult(Enumerable.Empty<object>()),
            Follow = true
        };
        spider.AddRule(rule);

        var html = "<html><body><a href=\"/link\">Link</a></body></html>";
        var response = TestData.CreateResponse("https://example.com/page", html, contentType: ContentType.Html);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        var pendingRequests = spider.GetPendingRequests(context).ToList();
        pendingRequests.Should().HaveCount(2);
        pendingRequests.Select(r => r.Url).Should().NotContain("https://blocked.com/page");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rule_DefaultPattern_MatchesAllUrls()
    {
        // Arrange
        var rule = new Rule();

        // Act & Assert
        rule.Pattern.IsMatch("https://example.com").Should().BeTrue();
        rule.Pattern.IsMatch("https://test.com/page").Should().BeTrue();
        rule.Pattern.IsMatch("http://localhost:8080").Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task Rule_DefaultCallback_ReturnsEmptyCollection()
    {
        // Arrange
        var rule = new Rule();
        var response = TestData.CreateResponse();

        // Act
        var result = await rule.Callback(response);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Rule_DefaultFollow_IsTrue()
    {
        // Arrange & Act
        var rule = new Rule();

        // Assert
        rule.Follow.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPendingRequests_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);

        // Act & Assert
        var act = () => spider.GetPendingRequests(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetExtractedItems_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);

        // Act & Assert
        var act = () => spider.GetExtractedItems(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetPendingRequests_WithEmptyQueue_ReturnsEmpty()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        var requests = spider.GetPendingRequests(context);

        // Assert
        requests.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetExtractedItems_WithNoItems_ReturnsEmpty()
    {
        // Arrange
        var spider = new TestCrawlSpider(_mockLinkExtractor);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        var items = spider.GetExtractedItems(context);

        // Assert
        items.Should().BeEmpty();
    }
}

/// <summary>
/// Test implementation of CrawlSpider for unit testing
/// </summary>
internal sealed class TestCrawlSpider : CrawlSpider
{
    public override string Name => "TestCrawlSpider";
    public List<string> AllowedDomains { get; set; } = [];

    public TestCrawlSpider(ILinkExtractor linkExtractor) : base(linkExtractor)
    {
        Options.AllowedDomains.Clear();
    }

    public override IEnumerable<string> GetStartUrls()
    {
        return new[] { "https://example.com" };
    }

    public override bool ShouldFollowUrl(string url, SpiderExecutionContext context)
    {
        if (AllowedDomains.Count == 0)
        {
            return base.ShouldFollowUrl(url, context);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host.ToLowerInvariant();
        return AllowedDomains.Any(d => host.Contains(d.ToLowerInvariant(), StringComparison.Ordinal));
    }
}

/// <summary>
/// Test link extractor for unit tests
/// </summary>
internal sealed class TestLinkExtractor : ILinkExtractor
{
    private IEnumerable<string> _links = Enumerable.Empty<string>();
    private int _callCount;

    public bool DidNotExtractLinks => _callCount == 0;

    public void SetLinks(IEnumerable<string> links)
    {
        _links = links;
    }

    public IEnumerable<string> ExtractLinks(string html, string baseUrl)
    {
        _callCount++;
        return _links;
    }
}

/// <summary>
/// Test entity for unit tests
/// </summary>
internal sealed class TestEntity
{
    public string? Id { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }
}
