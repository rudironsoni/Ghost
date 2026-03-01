using FluentAssertions;
using Ghost.Sdk.Spider.Engine;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Xunit;
using SpiderExecutionContext = Ghost.Sdk.Spider.Engine.ExecutionContext;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Unit.Engine;

public class SpiderTests : ReliabilityTestBase
{
    public SpiderTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void GetStartUrls_ShouldReturnConfiguredUrls()
    {
        // Arrange
        var expectedUrls = new List<string> { "https://example.com", "https://test.com" };
        var spider = new TestSpider(expectedUrls);

        // Act
        var urls = spider.GetStartUrls();

        // Assert
        urls.Should().BeEquivalentTo(expectedUrls);
    }

    [Fact]
    public async Task ProcessResponseAsync_ShouldBeInvoked()
    {
        // Arrange
        var spider = new TestSpider();
        var response = TestData.CreateResponse();
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        spider.ProcessedResponses.Should().HaveCount(1);
        spider.ProcessedResponses[0].Should().BeSameAs(response);
    }

    [Fact]
    public async Task OnStartAsync_ShouldBeCallable()
    {
        // Arrange
        var spider = new ConfigurableTestSpider();
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.OnStartAsync(context);

        // Assert
        spider.OnStartCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnCompleteAsync_ShouldBeCallable()
    {
        // Arrange
        var spider = new ConfigurableTestSpider();
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());
        var result = new SpiderResult { SpiderName = "Test", Success = true };

        // Act
        await spider.OnCompleteAsync(context, result);

        // Assert
        spider.OnCompleteCalled.Should().BeTrue();
    }

    [Fact]
    public async Task OnErrorAsync_ShouldCaptureException()
    {
        // Arrange
        var spider = new ConfigurableTestSpider();
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());
        var exception = new InvalidOperationException("Test error");

        // Act
        await spider.OnErrorAsync(exception, context);

        // Assert
        spider.ErrorsReceived.Should().HaveCount(1);
        spider.ErrorsReceived[0].Should().BeSameAs(exception);
    }

    [Fact]
    public void ShouldFollowUrl_WithValidUrl_ShouldReturnTrue()
    {
        // Arrange
        var spider = new ConfigurableTestSpider();
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        var shouldFollow = spider.ShouldFollowUrl("https://test.com/page", context);

        // Assert
        shouldFollow.Should().BeTrue();
    }

    [Fact]
    public void ShouldFollowUrl_WithNullUrl_ShouldReturnFalse()
    {
        // Arrange
        var spider = new ConfigurableTestSpider();
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        var shouldFollow = spider.ShouldFollowUrl(null!, context);

        // Assert
        shouldFollow.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrl_WithInvalidUrl_ShouldReturnFalse()
    {
        // Arrange
        var spider = new ConfigurableTestSpider();
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        var shouldFollow = spider.ShouldFollowUrl("not-a-valid-url", context);

        // Assert
        shouldFollow.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrl_WithAllowedDomains_ShouldFilterByDomain()
    {
        // Arrange
        var options = new SpiderOptions();
        options.AllowedDomains.Add("example.com");

        var spider = new ConfigurableTestSpider(options: options);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        var shouldFollowAllowed = spider.ShouldFollowUrl("https://example.com/page", context);
        var shouldFollowDisallowed = spider.ShouldFollowUrl("https://other.com/page", context);

        // Assert
        shouldFollowAllowed.Should().BeTrue();
        shouldFollowDisallowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrl_WithExcludePatterns_ShouldFilterByPattern()
    {
        // Arrange
        var options = new SpiderOptions();
        options.ExcludePatterns.Add(@".*/admin/.*");

        var spider = new ConfigurableTestSpider(options: options);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        var shouldFollowNormal = spider.ShouldFollowUrl("https://example.com/page", context);
        var shouldFollowAdmin = spider.ShouldFollowUrl("https://example.com/admin/users", context);

        // Assert
        shouldFollowNormal.Should().BeTrue();
        shouldFollowAdmin.Should().BeFalse();
    }

    [Fact]
    public void ShouldFollowUrl_WithCustomLogic_ShouldUseCustomLogic()
    {
        // Arrange
        var spider = new ConfigurableTestSpider();
        spider.ShouldFollowFunc = (url, ctx) => url.Contains("allowed");
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        var shouldFollow1 = spider.ShouldFollowUrl("https://example.com/allowed", context);
        var shouldFollow2 = spider.ShouldFollowUrl("https://example.com/denied", context);

        // Assert
        shouldFollow1.Should().BeTrue();
        shouldFollow2.Should().BeFalse();
    }

    [Fact]
    public async Task ProcessResponseAsync_WithCallback_ShouldInvokeCallback()
    {
        // Arrange
        var callbackInvoked = false;
        var spider = new TestSpider(processCallback: (response, context) =>
        {
            callbackInvoked = true;
        });

        var response = TestData.CreateResponse();
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        await spider.ProcessResponseAsync(response, context);

        // Assert
        callbackInvoked.Should().BeTrue();
    }

    [Fact]
    public void Name_ShouldReturnSpiderName()
    {
        // Arrange
        var spider = new ConfigurableTestSpider("MyCustomSpider");

        // Assert
        spider.Name.Should().Be("MyCustomSpider");
    }

    [Fact]
    public void Options_ShouldReturnSpiderOptions()
    {
        // Arrange
        var customOptions = new SpiderOptions();
        customOptions.AllowedDomains.Add("example.com");

        var spider = new ConfigurableTestSpider(options: customOptions);

        // Assert
        spider.Options.Should().BeSameAs(customOptions);
        spider.Options.AllowedDomains.Should().Contain("example.com");
    }
}
