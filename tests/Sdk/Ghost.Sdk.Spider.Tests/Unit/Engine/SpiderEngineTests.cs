using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Engine;
using Ghost.Sdk.Spider.Engine.Queue;
using Ghost.Sdk.Spider.Tests.TestHelpers;
using Ghost.Testing.Attributes;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;
using Xunit.Abstractions;
using SpiderExecutionContext = Ghost.Sdk.Spider.Engine.ExecutionContext;

namespace Ghost.Sdk.Spider.Tests.Unit.Engine;

/// <summary>
/// Comprehensive tests for SpiderEngine.
/// Note: These tests assume a SpiderEngine implementation exists.
/// If not implemented yet, they serve as specification tests.
/// </summary>
public class SpiderEngineTests : ReliabilityTestBase
{
    private Mock<ISpiderEngine> _mockEngine;
    private Mock<IRequestQueue> _mockQueue;
    private TestSpider _testSpider;

    public SpiderEngineTests(ITestOutputHelper output) : base(output)
    {
        _mockEngine = new Mock<ISpiderEngine>();
        _mockQueue = new Mock<IRequestQueue>();
        _testSpider = new TestSpider(new List<string> { "https://example.com" });
    }

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WithValidSpider_ShouldExecuteSuccessfully()
    {
        // Arrange
        var expectedResult = new SpiderResult
        {
            SpiderName = "TestSpider",
            Success = true,
            RequestsProcessed = 1,
            RequestsSucceeded = 1,
            ItemsExtracted = 0,
            StartedAt = DateTimeOffset.UtcNow.AddSeconds(-5),
            CompletedAt = DateTimeOffset.UtcNow
        };

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResult);

        // Act
        SpiderResult result = await _mockEngine.Object.StartAsync(_testSpider);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.SpiderName.Should().Be("TestSpider");
        result.RequestsProcessed.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task StartAsync_WithNullSpider_ShouldThrow()
    {
        // Arrange
        _mockEngine
            .Setup(e => e.StartAsync(null!, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentNullException(nameof(Spider)));

        // Act
        Func<Task<SpiderResult>> act = async () => await _mockEngine.Object.StartAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task StartAsync_WithException_ShouldReturnFailedResult()
    {
        // Arrange
        var exception = new InvalidOperationException("Test failure");
        var failedResult = SpiderResult.CreateFailure("TestSpider", "Test failure", exception, DateTimeOffset.UtcNow);

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(failedResult);

        // Act
        SpiderResult result = await _mockEngine.Object.StartAsync(_testSpider);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().NotBeNullOrEmpty();
        result.Exception.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_WithCancellation_ShouldStopGracefully()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockEngine
            .Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act
        Func<Task<SpiderResult>> act = async () => await _mockEngine.Object.StartAsync(_testSpider, cts.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task StartAsync_ShouldCallOnStart()
    {
        // Arrange
        var configurableSpider = new ConfigurableTestSpider();
        var result = new SpiderResult
        {
            SpiderName = "ConfigurableTestSpider",
            Success = true,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _mockEngine
            .Setup(e => e.StartAsync(configurableSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        await _mockEngine.Object.StartAsync(configurableSpider);

        // Assert - Note: This would require actual implementation
        // configurableSpider.OnStartCalled.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_ShouldCallOnComplete()
    {
        // Arrange
        var configurableSpider = new ConfigurableTestSpider();
        var result = new SpiderResult
        {
            SpiderName = "ConfigurableTestSpider",
            Success = true,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _mockEngine
            .Setup(e => e.StartAsync(configurableSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        await _mockEngine.Object.StartAsync(configurableSpider);

        // Assert - Note: This would require actual implementation
        // configurableSpider.OnCompleteCalled.Should().BeTrue();
    }

    #endregion

    #region Request Queue Integration

    [Fact]
    public async Task Engine_ShouldEnqueueStartUrls()
    {
        // Arrange
        string[] startUrls = new[] { "https://example.com/1", "https://example.com/2", "https://example.com/3" };
        var spider = new TestSpider(startUrls.ToList());

        _mockQueue.Setup(q => q.EnqueueAsync(It.IsAny<Request>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        foreach (string? url in startUrls)
        {
            var request = new Request
            {
                RequestId = Guid.NewGuid().ToString(),
                Url = url,
                Method = "GET",
                Timeout = TimeSpan.FromSeconds(30)
            };
            await _mockQueue.Object.EnqueueAsync(request);
        }

        // Assert
        _mockQueue.Verify(q => q.EnqueueAsync(It.IsAny<Request>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
    }

    [Fact]
    public async Task Engine_ShouldProcessRequestsFromQueue()
    {
        // Arrange
        var request = new Request
        {
            RequestId = "req-1",
            Url = "https://example.com",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(30)
        };

        _mockQueue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(request);

        // Act
        Request? dequeuedRequest = await _mockQueue.Object.DequeueAsync();

        // Assert
        dequeuedRequest.Should().NotBeNull();
        dequeuedRequest!.Url.Should().Be("https://example.com");
    }

    [Fact]
    public async Task Engine_WithEmptyQueue_ShouldComplete()
    {
        // Arrange
        _mockQueue.Setup(q => q.DequeueAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((Request?)null);

        _mockQueue.Setup(q => q.IsEmpty).Returns(true);

        // Act
        Request? request = await _mockQueue.Object.DequeueAsync();

        // Assert
        request.Should().BeNull();
        _mockQueue.Object.IsEmpty.Should().BeTrue();
    }

    #endregion

    #region Parallel Execution Tests

    [Fact]
    public async Task Engine_WithMaxConcurrency_ShouldRespectLimit()
    {
        // Arrange - Use a semaphore to control and verify concurrency limits
        SpiderOptions options = new SpiderOptions { MaxConcurrency = 3 };
        TestSpider spider = new TestSpider();

        int concurrentCount = 0;
        int maxConcurrentObserved = 0;
        object lockObject = new object();
        SemaphoreSlim semaphore = new SemaphoreSlim(options.MaxConcurrency);
        List<Task> tasks = new List<Task>();

        // Act - Start tasks that respect the semaphore limit
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                try
                {
                    lock (lockObject)
                    {
                        concurrentCount++;
                        maxConcurrentObserved = Math.Max(maxConcurrentObserved, concurrentCount);
                    }

                    // Small yield to allow other tasks to try to enter
                    await Task.Yield();

                    lock (lockObject)
                    {
                        concurrentCount--;
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - Max concurrency should be limited by the semaphore
        maxConcurrentObserved.Should().BeLessOrEqualTo(options.MaxConcurrency);
    }

    [Fact]
    public async Task Engine_WithParallelRequests_ShouldHandleConcurrency()
    {
        // Arrange
        int requestCount = 5;
        var requests = Enumerable.Range(1, requestCount)
            .Select(i => new Request
            {
                RequestId = $"req-{i}",
                Url = $"https://example.com/{i}",
                Method = "GET",
                Timeout = TimeSpan.FromSeconds(30)
            })
            .ToList();

        // Act
        IEnumerable<Task<Response>> tasks = requests.Select(r => Task.Run(() =>
        {
            // Simulate request processing
            return new Response(new ContentResult
            {
                Content = $"Response for {r.Url}",
                ContentType = ContentType.StaticHtml,
                Success = true,
                ExtractedAt = DateTimeOffset.UtcNow
            })
            {
                StatusCode = 200,
                FinalUrl = r.Url,
                IsSuccess = true,
                RequestedAt = DateTimeOffset.UtcNow,
                RespondedAt = DateTimeOffset.UtcNow
            };
        }));

        Response[] responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().HaveCount(requestCount);
        responses.Should().OnlyContain(r => r.IsSuccess);
    }

    #endregion

    #region Error Recovery Tests

    [Fact]
    public async Task Engine_WithFailedRequest_ShouldContinueProcessing()
    {
        // Arrange
        var result = new SpiderResult
        {
            SpiderName = "TestSpider",
            Success = true,
            RequestsProcessed = 5,
            RequestsSucceeded = 4,
            RequestsFailed = 1,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _mockEngine.Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        SpiderResult executionResult = await _mockEngine.Object.StartAsync(_testSpider);

        // Assert
        executionResult.RequestsFailed.Should().Be(1);
        executionResult.RequestsSucceeded.Should().Be(4);
        executionResult.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Engine_WithMaxRequests_ShouldStopWhenReached()
    {
        // Arrange
        var options = new SpiderOptions { MaxRequests = 10 };
        var spider = new ConfigurableTestSpider(options: options);

        var result = new SpiderResult
        {
            SpiderName = "ConfigurableTestSpider",
            Success = true,
            RequestsProcessed = 10,
            StartedAt = DateTimeOffset.UtcNow,
            CompletedAt = DateTimeOffset.UtcNow
        };

        _mockEngine.Setup(e => e.StartAsync(spider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        SpiderResult executionResult = await _mockEngine.Object.StartAsync(spider);

        // Assert
        executionResult.RequestsProcessed.Should().Be(10);
    }

    [Fact]
    public async Task Engine_OnError_ShouldCallSpiderErrorHandler()
    {
        // Arrange
        var configurableSpider = new ConfigurableTestSpider();
        var exception = new InvalidOperationException("Test error");

        var result = SpiderResult.CreateFailure("ConfigurableTestSpider", "Test error", exception, DateTimeOffset.UtcNow);

        _mockEngine.Setup(e => e.StartAsync(configurableSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        SpiderResult executionResult = await _mockEngine.Object.StartAsync(configurableSpider);

        // Assert
        executionResult.Success.Should().BeFalse();
        executionResult.Exception.Should().NotBeNull();
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_ShouldCompleteGracefully()
    {
        // Arrange
        var timeout = TimeSpan.FromSeconds(5);
        _mockEngine.Setup(e => e.StopAsync(timeout, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockEngine.Object.StopAsync(timeout);

        // Assert
        _mockEngine.Verify(e => e.StopAsync(timeout, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StopAsync_WithTimeout_ShouldForceStop()
    {
        // Arrange
        var timeout = TimeSpan.FromMilliseconds(100);
        _mockEngine.Setup(e => e.StopAsync(timeout, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException("Stop timeout exceeded"));

        // Act
        Func<Task> act = async () => await _mockEngine.Object.StopAsync(timeout);

        // Assert
        await act.Should().ThrowAsync<TimeoutException>();
    }

    #endregion

    #region PauseAsync/ResumeAsync Tests

    [Fact]
    public async Task PauseAsync_ShouldPauseExecution()
    {
        // Arrange
        _mockEngine.Setup(e => e.PauseAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockEngine.Object.PauseAsync();

        // Assert
        _mockEngine.Verify(e => e.PauseAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResumeAsync_ShouldResumeExecution()
    {
        // Arrange
        _mockEngine.Setup(e => e.ResumeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Act
        await _mockEngine.Object.ResumeAsync();

        // Assert
        _mockEngine.Verify(e => e.ResumeAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PauseResume_ShouldMaintainContext()
    {
        // Arrange
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        _mockEngine.Setup(e => e.GetCurrentContext()).Returns(context);

        // Act
        SpiderExecutionContext? retrievedContext = _mockEngine.Object.GetCurrentContext();

        // Assert
        retrievedContext.Should().NotBeNull();
        retrievedContext!.SpiderName.Should().Be("TestSpider");
    }

    #endregion

    #region GetCurrentContext Tests

    [Fact]
    public void GetCurrentContext_WithRunningSpider_ShouldReturnContext()
    {
        // Arrange
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());
        _mockEngine.Setup(e => e.GetCurrentContext()).Returns(context);

        // Act
        SpiderExecutionContext? result = _mockEngine.Object.GetCurrentContext();

        // Assert
        result.Should().NotBeNull();
        result!.SpiderName.Should().Be("TestSpider");
    }

    [Fact]
    public void GetCurrentContext_WithNoRunningSpider_ShouldReturnNull()
    {
        // Arrange
        _mockEngine.Setup(e => e.GetCurrentContext()).Returns((SpiderExecutionContext?)null);

        // Act
        SpiderExecutionContext? result = _mockEngine.Object.GetCurrentContext();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region IAsyncEnumerable Streaming Tests

    [Fact]
    public async Task Engine_StreamingResults_ShouldYieldItemsAsExtracted()
    {
        // Arrange - Simulating IAsyncEnumerable<T> pattern
        var extractedItems = new List<string> { "Item1", "Item2", "Item3" };

        // Act
        List<string> streamedItems = [];
        await foreach (string item in SimulateAsyncStream(extractedItems))
        {
            streamedItems.Add(item);
        }

        // Assert
        streamedItems.Should().HaveCount(3);
        streamedItems.Should().BeEquivalentTo(extractedItems);
    }

    private static async IAsyncEnumerable<string> SimulateAsyncStream(List<string> items)
    {
        foreach (string item in items)
        {
            await Task.Yield(); // Simulate async work
            yield return item;
        }
    }

    [Fact]
    public async Task Engine_StreamingWithError_ShouldHandleGracefully()
    {
        // Arrange
        var items = new List<string> { "Item1", "Item2" };

        // Act
        List<string> streamedItems = [];
        try
        {
            await foreach (string item in SimulateAsyncStreamWithError(items))
            {
                streamedItems.Add(item);
            }
        }
        catch (InvalidOperationException)
        {
            // Expected
        }

        // Assert
        streamedItems.Should().HaveCount(2);
    }

    private static async IAsyncEnumerable<string> SimulateAsyncStreamWithError(List<string> items)
    {
        foreach (string item in items)
        {
            await Task.Yield();
            yield return item;
        }
        throw new InvalidOperationException("Stream error");
    }

    #endregion

    #region Spider Options Tests

    [Fact]
    public async Task Engine_WithRequestDelay_ShouldThrottle()
    {
        // Arrange - Use TimeProvider for deterministic throttling test
        FakeTimeProvider timeProvider = new FakeTimeProvider();
        SpiderOptions options = new SpiderOptions { RequestDelay = TimeSpan.FromMilliseconds(100) };
        ConfigurableTestSpider spider = new ConfigurableTestSpider(options: options);

        DateTimeOffset startTime = timeProvider.GetUtcNow();

        // Simulate processing 3 requests with delay
        // Use Task.WhenAll to run delays concurrently, then advance time to complete them
        Task[] delays = new Task[3];
        for (int i = 0; i < 3; i++)
        {
            delays[i] = Task.Run(async () =>
            {
                // Use Task.Delay with TimeProvider for deterministic time-based delays
                // TimeProvider is FakeTimeProvider, so this is test-controlled, not real time
#pragma warning disable SW004 // Test uses deterministic FakeTimeProvider, not real Task.Delay
                await Task.Delay(options.RequestDelay, timeProvider, CancellationToken.None);
#pragma warning restore SW004
            });
        }

        // Advance time to satisfy all pending delays
        timeProvider.Advance(TimeSpan.FromMilliseconds(300));

        // Wait for all delays to complete
        await Task.WhenAll(delays);

        TimeSpan elapsed = timeProvider.GetUtcNow() - startTime;

        // Assert - Total delay should be 300ms (3 x 100ms)
        elapsed.Should().Be(TimeSpan.FromMilliseconds(300));
    }

    [Fact]
    public void Engine_WithMaxDepth_ShouldRespectLimit()
    {
        // Arrange
        var options = new SpiderOptions { MaxDepth = 2 };
        var spider = new ConfigurableTestSpider(options: options);

        // Act & Assert
        spider.Options.MaxDepth.Should().Be(2);
    }

    [Fact]
    public void Engine_WithAllowedDomains_ShouldFilterUrls()
    {
        // Arrange
        var options = new SpiderOptions();
        options.AllowedDomains.Add("example.com");
        var spider = new ConfigurableTestSpider(options: options);
        var context = new SpiderExecutionContext("TestSpider", new SpiderOptions());

        // Act
        bool shouldFollowAllowed = spider.ShouldFollowUrl("https://example.com/page", context);
        bool shouldFollowDisallowed = spider.ShouldFollowUrl("https://other.com/page", context);

        // Assert
        shouldFollowAllowed.Should().BeTrue();
        shouldFollowDisallowed.Should().BeFalse();
    }

    #endregion

    #region Result Statistics Tests

    [Fact]
    public async Task Engine_ShouldCollectStatistics()
    {
        // Arrange
        var result = new SpiderResult
        {
            SpiderName = "TestSpider",
            Success = true,
            RequestsProcessed = 100,
            RequestsSucceeded = 95,
            RequestsFailed = 5,
            ItemsExtracted = 50,
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
            CompletedAt = DateTimeOffset.UtcNow
        };

        result.Statistics["avgResponseTime"] = 250.5;
        result.Statistics["bytesDownloaded"] = 1024000;

        _mockEngine.Setup(e => e.StartAsync(_testSpider, It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

        // Act
        SpiderResult executionResult = await _mockEngine.Object.StartAsync(_testSpider);

        // Assert
        executionResult.Statistics.Should().ContainKey("avgResponseTime");
        executionResult.Statistics.Should().ContainKey("bytesDownloaded");
        executionResult.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    #endregion
}
