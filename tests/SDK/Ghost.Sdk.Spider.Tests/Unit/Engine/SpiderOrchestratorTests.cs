using FluentAssertions;
using Ghost.Sdk.Spider.Engine;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Moq;
using NUnit.Framework;
using SpiderExecutionContext = Ghost.Sdk.Spider.Engine.ExecutionContext;

namespace Ghost.Sdk.Spider.Tests.Unit.Engine;

/// <summary>
/// Tests for spider orchestration and coordination
/// </summary>
[TestFixture]
public class SpiderOrchestratorTests
{
    private Mock<ISpiderEngine> _mockEngine = null!;
    private SpiderOptions _options = null!;

    [SetUp]
    public void Setup()
    {
        _mockEngine = new Mock<ISpiderEngine>();
        _options = new SpiderOptions
        {
            MaxConcurrency = 2,
            MaxRequests = 10,
            RequestDelay = TimeSpan.FromMilliseconds(10)
        };
    }

    [Test]
    public async Task Orchestrator_ExecutesSpiderLifecycle()
    {
        // Arrange
        var spider = new ConfigurableTestSpider("OrchestrationSpider", _options);
        var startUrls = new[] { "https://test.com/page1", "https://test.com/page2" };
        spider.GetStartUrlsFunc = () => startUrls;

        var expectedResult = new SpiderResult
        {
            SpiderName = "OrchestrationSpider",
            Success = true,
            RequestsProcessed = 2,
            RequestsSucceeded = 2,
            ItemsExtracted = 0
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RequestsProcessed.Should().Be(2);
    }

    [Test]
    public async Task Orchestrator_HandlesEmptyStartUrls()
    {
        // Arrange
        var spider = new ConfigurableTestSpider("EmptyUrlsSpider", _options);
        spider.GetStartUrlsFunc = () => Array.Empty<string>();

        var expectedResult = new SpiderResult
        {
            SpiderName = "EmptyUrlsSpider",
            Success = true,
            RequestsProcessed = 0
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.RequestsProcessed.Should().Be(0);
    }

    [Test]
    public async Task Orchestrator_PropagatesErrors()
    {
        // Arrange
        var spider = new ConfigurableTestSpider("ErrorSpider", _options);
        
        var expectedResult = new SpiderResult
        {
            SpiderName = "ErrorSpider",
            Success = false,
            Error = "Test error occurred"
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("error");
    }

    [Test]
    public async Task Orchestrator_RespectsMaxRequests()
    {
        // Arrange
        var limitedOptions = new SpiderOptions { MaxRequests = 3, MaxConcurrency = 1 };
        var spider = new ConfigurableTestSpider("LimitedSpider", limitedOptions);

        var expectedResult = new SpiderResult
        {
            SpiderName = "LimitedSpider",
            Success = true,
            RequestsProcessed = 3
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);

        // Assert
        result.RequestsProcessed.Should().Be(3);
    }

    [Test]
    public async Task Orchestrator_HandlesCancellation()
    {
        // Arrange
        var spider = new ConfigurableTestSpider("CancellableSpider", _options);
        var cts = new CancellationTokenSource();

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await FluentActions.Awaiting(() => _mockEngine.Object.StartAsync(spider, cts.Token))
            .Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task Orchestrator_TracksStatistics()
    {
        // Arrange
        var spider = new ConfigurableTestSpider("StatsSpider", _options);

        var expectedResult = new SpiderResult
        {
            SpiderName = "StatsSpider",
            Success = true,
            RequestsProcessed = 5,
            RequestsSucceeded = 4,
            RequestsFailed = 1,
            ItemsExtracted = 20
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);

        // Assert
        result.RequestsProcessed.Should().Be(5);
        result.RequestsSucceeded.Should().Be(4);
        result.RequestsFailed.Should().Be(1);
        result.ItemsExtracted.Should().Be(20);
    }

    [Test]
    public async Task Orchestrator_HandlesEngineFailure()
    {
        // Arrange
        var spider = new ConfigurableTestSpider("FailureSpider", _options);

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        // Act & Assert
        await FluentActions.Awaiting(() => _mockEngine.Object.StartAsync(spider, CancellationToken.None))
            .Should().ThrowAsync<HttpRequestException>();
    }

    [Test]
    public async Task Orchestrator_SupportsExecutionContext()
    {
        // Arrange
        var context = new SpiderExecutionContext("TestSpider", _options);
        
        _mockEngine.Setup(e => e.GetCurrentContext())
            .Returns(context);

        // Act
        var retrievedContext = _mockEngine.Object.GetCurrentContext();

        // Assert
        retrievedContext.Should().NotBeNull();
        retrievedContext!.SpiderName.Should().Be("TestSpider");
    }
}
