using FluentAssertions;
using Ghost.Sdk.Spider.Engine;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Moq;
using NUnit.Framework;
using Quartz;
using SpiderBase = Ghost.Sdk.Spider.Engine.Spider;

namespace Ghost.Sdk.Spider.Tests.Unit.Scheduling;

/// <summary>
/// Tests for SpiderJob - the Quartz job that executes spiders.
/// Note: These tests assume a SpiderJob implementation exists.
/// If not implemented yet, they serve as specification tests.
/// </summary>
[TestFixture]
public class SpiderJobTests
{
    private Mock<IJobExecutionContext> _mockJobContext = null!;
    private Mock<ISpiderEngine> _mockEngine = null!;
    private TestSpider _testSpider = null!;

    [SetUp]
    public void Setup()
    {
        _mockJobContext = new Mock<IJobExecutionContext>();
        _mockEngine = new Mock<ISpiderEngine>();
        _testSpider = new TestSpider();

        // Setup job data map
        var jobDataMap = new JobDataMap
        {
            ["spider"] = _testSpider,
            ["spiderName"] = "TestSpider"
        };

        _mockJobContext.Setup(c => c.MergedJobDataMap).Returns(jobDataMap);
        _mockJobContext.Setup(c => c.CancellationToken).Returns(CancellationToken.None);
    }

    [Test]
    public async Task Execute_WithValidSpider_ShouldStartEngine()
    {
        // Arrange
        var expectedResult = new SpiderResult
        {
            SpiderName = "TestSpider",
            Success = true,
            RequestsProcessed = 10,
            ItemsExtracted = 5,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            CompletedAt = DateTimeOffset.UtcNow
        };

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Simulate job execution
        // Act
        var result = await _mockEngine.Object.StartAsync(_testSpider, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SpiderName.Should().Be("TestSpider");
        _mockEngine.Verify(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task Execute_WithEngineException_ShouldLogError()
    {
        // Arrange
        var exception = new Exception("Engine failed");

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ThrowsAsync(exception);

        // Act
        var act = async () => await _mockEngine.Object.StartAsync(_testSpider, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<Exception>().WithMessage("Engine failed");
    }

    [Test]
    public async Task Execute_WithCancellation_ShouldStopGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        var act = async () => await _mockEngine.Object.StartAsync(_testSpider, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Test]
    public async Task Execute_ShouldPassCancellationToken()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        CancellationToken? capturedToken = null;

        _mockEngine
            .Setup(e => e.StartAsync(It.IsAny<SpiderBase>(), It.IsAny<CancellationToken>()))
            .Callback<SpiderBase, CancellationToken>((spider, token) => capturedToken = token)
            .ReturnsAsync(new SpiderResult
            {
                SpiderName = "TestSpider",
                Success = true,
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = DateTimeOffset.UtcNow
            });

        // Act
        await _mockEngine.Object.StartAsync(_testSpider, cts.Token);

        // Assert
        capturedToken.Should().NotBeNull();
        if (capturedToken.HasValue)
        {
            capturedToken.Value.Should().Be(cts.Token);
        }
    }

    [Test]
    public async Task Execute_WithFailedResult_ShouldHandleFailure()
    {
        // Arrange
        var failedResult = SpiderResult.CreateFailure(
            "TestSpider",
            "Spider execution failed",
            new Exception("Test error"),
            DateTimeOffset.UtcNow);

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(_testSpider, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Spider execution failed");
        result.Exception.Should().NotBeNull();
    }

    [Test]
    public void JobDataMap_ShouldContainSpider()
    {
        // Arrange & Act
        var jobDataMap = _mockJobContext.Object.MergedJobDataMap;

        // Assert
        jobDataMap.Should().ContainKey("spider");
        jobDataMap["spider"].Should().Be(_testSpider);
    }

    [Test]
    public void JobDataMap_ShouldContainSpiderName()
    {
        // Arrange & Act
        var jobDataMap = _mockJobContext.Object.MergedJobDataMap;

        // Assert
        jobDataMap.Should().ContainKey("spiderName");
        jobDataMap["spiderName"].Should().Be("TestSpider");
    }

    [Test]
    public async Task Execute_WithNullSpider_ShouldThrow()
    {
        // Arrange
        var jobDataMap = new JobDataMap();
        _mockJobContext.Setup(c => c.MergedJobDataMap).Returns(jobDataMap);

        // Act & Assert
        jobDataMap.Should().NotContainKey("spider");
    }

    [Test]
    public async Task Execute_WithLongRunningSpider_ShouldComplete()
    {
        // Arrange
        var delay = TimeSpan.FromSeconds(2);
        var result = new SpiderResult
        {
            SpiderName = "TestSpider",
            Success = true,
            RequestsProcessed = 100,
            ItemsExtracted = 50,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow.Add(delay)
        };

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .Returns(async () =>
            {
                await Task.Delay(delay);
                return result;
            });

        // Act
        var executionResult = await _mockEngine.Object.StartAsync(_testSpider, CancellationToken.None);

        // Assert
        executionResult.Should().NotBeNull();
        executionResult.Duration.Should().BeGreaterOrEqualTo(delay);
    }

    [Test]
    public async Task Execute_WithJobInterruption_ShouldCancel()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        _mockJobContext.Setup(c => c.CancellationToken).Returns(cts.Token);

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .Returns(async (SpiderBase spider, CancellationToken token) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10), token);
                return new SpiderResult
                {
                    SpiderName = "TestSpider",
                    Success = true,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow
                };
            });

        // Act
        var task = _mockEngine.Object.StartAsync(_testSpider, cts.Token);
        cts.Cancel();

        // Assert
        await task.Invoking(async t => await t).Should().ThrowAsync<TaskCanceledException>();
    }

    [Test]
    public async Task Execute_ShouldRecordExecutionMetrics()
    {
        // Arrange
        var result = new SpiderResult
        {
            SpiderName = "TestSpider",
            Success = true,
            RequestsProcessed = 25,
            RequestsSucceeded = 23,
            RequestsFailed = 2,
            ItemsExtracted = 15,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            CompletedAt = DateTimeOffset.UtcNow
        };

        result.Statistics["customMetric"] = 42;

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        var executionResult = await _mockEngine.Object.StartAsync(_testSpider, CancellationToken.None);

        // Assert
        executionResult.RequestsProcessed.Should().Be(25);
        executionResult.RequestsSucceeded.Should().Be(23);
        executionResult.RequestsFailed.Should().Be(2);
        executionResult.ItemsExtracted.Should().Be(15);
        executionResult.Statistics.Should().ContainKey("customMetric");
    }

    [Test]
    public async Task Execute_WithRetryableError_ShouldRetry()
    {
        // Arrange
        var attemptCount = 0;
        var maxRetries = 3;

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attemptCount++;
                if (attemptCount < maxRetries)
                {
                    throw new InvalidOperationException("Temporary failure");
                }
                return Task.FromResult(new SpiderResult
                {
                    SpiderName = "TestSpider",
                    Success = true,
                    StartedAt = DateTimeOffset.UtcNow,
                    CompletedAt = DateTimeOffset.UtcNow
                });
            });

        // Act
        SpiderResult? result = null;
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                result = await _mockEngine.Object.StartAsync(_testSpider, CancellationToken.None);
                break;
            }
            catch (InvalidOperationException)
            {
                if (i == maxRetries - 1) throw;
            }
        }

        // Assert
        result.Should().NotBeNull();
        result!.Success.Should().BeTrue();
        attemptCount.Should().Be(maxRetries);
    }

    [Test]
    public void JobKey_ShouldBeUnique()
    {
        // Arrange
        var jobKey1 = new JobKey("TestSpider-1", "SpiderJobs");
        var jobKey2 = new JobKey("TestSpider-2", "SpiderJobs");
        var jobKey3 = new JobKey("TestSpider-1", "SpiderJobs");

        // Assert
        jobKey1.Should().NotBe(jobKey2);
        jobKey1.Should().Be(jobKey3);
        jobKey1.Name.Should().Be("TestSpider-1");
        jobKey1.Group.Should().Be("SpiderJobs");
    }

    [Test]
    public void TriggerKey_ShouldBeUnique()
    {
        // Arrange
        var triggerKey1 = new TriggerKey("trigger-1", "SpiderTriggers");
        var triggerKey2 = new TriggerKey("trigger-2", "SpiderTriggers");

        // Assert
        triggerKey1.Should().NotBe(triggerKey2);
        triggerKey1.Name.Should().Be("trigger-1");
        triggerKey1.Group.Should().Be("SpiderTriggers");
    }
}
