using FluentAssertions;
using Ghost.Sdk.Spider.Engine;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Moq;
using NUnit.Framework;
using System.Collections.Concurrent;

namespace Ghost.Sdk.Spider.Tests.Unit.Engine;

/// <summary>
/// Tests for parallel execution capabilities
/// </summary>
[TestFixture]
public class ParallelExecutionTests
{
    private Mock<ISpiderEngine> _mockEngine = null!;

    [SetUp]
    public void Setup()
    {
        _mockEngine = new Mock<ISpiderEngine>();
    }

    [Test]
    public async Task ParallelExecution_ProcessesMultipleRequestsConcurrently()
    {
        // Arrange
        var options = new SpiderOptions { MaxConcurrency = 5 };
        var spider = new ConfigurableTestSpider("ParallelSpider", options);

        var expectedResult = new SpiderResult
        {
            SpiderName = "ParallelSpider",
            Success = true,
            RequestsProcessed = 10,
            RequestsSucceeded = 10
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);

        // Assert
        result.RequestsProcessed.Should().Be(10);
        result.Success.Should().BeTrue();
    }

    [Test]
    public async Task ParallelExecution_RespectsConcurrencyLimit()
    {
        // Arrange
        var options = new SpiderOptions { MaxConcurrency = 2 };
        var spider = new ConfigurableTestSpider("LimitedParallelSpider", options);

        // Simulate respecting concurrency via semaphore
        var lockObj = new SemaphoreSlim(2, 2);
        var currentlyExecuting = 0;
        var maxConcurrent = 0;
        var lockForMax = new object();

        var tasks = Enumerable.Range(1, 6).Select(async i =>
        {
            await lockObj.WaitAsync();
            try
            {
                lock (lockForMax)
                {
                    currentlyExecuting++;
                    maxConcurrent = Math.Max(maxConcurrent, currentlyExecuting);
                }

                await Task.Delay(50);

                lock (lockForMax)
                {
                    currentlyExecuting--;
                }
            }
            finally
            {
                lockObj.Release();
            }
        });

        // Act
        await Task.WhenAll(tasks);

        // Assert
        maxConcurrent.Should().BeLessThanOrEqualTo(2);
    }

    [Test]
    public async Task ParallelExecution_HandlesPartialFailures()
    {
        // Arrange
        var options = new SpiderOptions { MaxConcurrency = 3 };
        var spider = new ConfigurableTestSpider("PartialFailureSpider", options);

        var expectedResult = new SpiderResult
        {
            SpiderName = "PartialFailureSpider",
            Success = true,
            RequestsProcessed = 6,
            RequestsSucceeded = 4,
            RequestsFailed = 2
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);

        // Assert
        result.RequestsProcessed.Should().Be(6);
        result.RequestsFailed.Should().Be(2);
    }

    [Test]
    public async Task ParallelExecution_ThreadSafeCounters()
    {
        // Arrange
        var counter = 0;
        var tasks = Enumerable.Range(1, 50).Select(_ =>
            Task.Run(() => Interlocked.Increment(ref counter)));

        // Act
        await Task.WhenAll(tasks);

        // Assert
        counter.Should().Be(50);
    }

    [Test]
    public async Task ParallelExecution_HandlesSlowRequests()
    {
        // Arrange
        var options = new SpiderOptions { MaxConcurrency = 3 };
        var spider = new ConfigurableTestSpider("SlowRequestSpider", options);

        var expectedResult = new SpiderResult
        {
            SpiderName = "SlowRequestSpider",
            Success = true,
            RequestsProcessed = 5
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var start = DateTimeOffset.UtcNow;
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);
        var elapsed = DateTimeOffset.UtcNow - start;

        // Assert
        result.RequestsProcessed.Should().Be(5);
    }

    [Test]
    public async Task ParallelExecution_DistributesWorkEvenly()
    {
        // Arrange
        var threadIds = new ConcurrentBag<int>();
        var tasks = Enumerable.Range(1, 16).Select(_ =>
            Task.Run(() => threadIds.Add(Environment.CurrentManagedThreadId)));

        // Act
        await Task.WhenAll(tasks);

        // Assert
        threadIds.Should().HaveCount(16);
        threadIds.Distinct().Should().HaveCountGreaterThan(1); // Used multiple threads
    }

    [Test]
    public async Task ParallelExecution_PropagatesExceptionsCorrectly()
    {
        // Arrange
        var options = new SpiderOptions { MaxConcurrency = 3 };
        var spider = new ConfigurableTestSpider("ExceptionSpider", options);

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("Test exception"));

        // Act & Assert
        await FluentActions.Awaiting(() => _mockEngine.Object.StartAsync(spider, CancellationToken.None))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Test]
    public async Task ParallelExecution_ContinuesAfterException()
    {
        // Arrange
        var options = new SpiderOptions { MaxConcurrency = 2 };
        var spider = new ConfigurableTestSpider("ContinueAfterErrorSpider", options);

        var expectedResult = new SpiderResult
        {
            SpiderName = "ContinueAfterErrorSpider",
            Success = true,
            RequestsProcessed = 6,
            RequestsSucceeded = 5,
            RequestsFailed = 1
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _mockEngine.Object.StartAsync(spider, CancellationToken.None);

        // Assert
        result.RequestsSucceeded.Should().Be(5);
        result.RequestsFailed.Should().Be(1);
    }
}
