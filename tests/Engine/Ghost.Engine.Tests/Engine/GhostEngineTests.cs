using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Pipelines;
using Ghost.Engine.Abstractions.Spider;
using Ghost.Engine.Abstractions.Transport;
using Ghost.Engine.Engine;
using Ghost.Engine.Scheduler;
using Xunit;

namespace Ghost.Engine.Tests.Engine;

public class GhostEngineTests
{
    private static readonly GhostRequest[] EmptyRequests = Array.Empty<GhostRequest>();
    private static readonly ItemEnvelope[] EmptyItems = Array.Empty<ItemEnvelope>();
    private static readonly string[] TestUrls = { "http://example.com/1", "http://example.com/2", "http://example.com/3" };

    [Fact]
    public async Task RunAsync_WithFakeDownloader_ProcessesRequestsDeterministicallyAsync()
    {
        // Arrange
        GhostEngineOptions options = new GhostEngineOptions
        {
            MaxInFlight = 2,
            MaxPendingItems = 10
        };

        List<string> processedRequests = [];
        List<string> processedItems = [];

        TestSpider spider = new TestSpider(processedRequests, processedItems);
        TestDownloader downloader = new TestDownloader();
        TestItemPipeline itemPipeline = new TestItemPipeline(processedItems);

        GhostEngine engine = new GhostEngine(
            options: options,
            scheduler: new InMemoryRequestScheduler(),
            downloader: downloader,
            downloaderMiddlewares: Array.Empty<IDownloaderMiddleware>(),
            spiderMiddlewares: Array.Empty<ISpiderMiddleware>(),
            itemPipelines: new[] { itemPipeline });

        GhostEngineContext context = new GhostEngineContext("test-job", "test-spider", new Dictionary<string, object?>());

        // Act
        await engine.RunAsync(spider, context);

        // Assert
        processedRequests.Should().HaveCount(3, "should process 3 start requests");
        processedRequests.Should().BeEquivalentTo(TestUrls,
            options => options.WithStrictOrdering());

        processedItems.Should().HaveCount(3, "should process 3 items");
    }

    [Fact]
    public async Task RunAsync_WithBackpressure_RespectsMaxInFlightAsync()
    {
        // Arrange
        GhostEngineOptions options = new GhostEngineOptions
        {
            MaxInFlight = 1,
            MaxPendingItems = 10
        };

        InFlightCounter counter = new InFlightCounter();
        TaskCompletionSource<GhostResponse> downloadBlocker = new TaskCompletionSource<GhostResponse>();

        SynchronousTestDownloader downloader = new SynchronousTestDownloader(counter, downloadBlocker);
        ControlledSpider spider = new ControlledSpider(3);

        GhostEngine engine = new GhostEngine(
            options: options,
            scheduler: new InMemoryRequestScheduler(),
            downloader: downloader);

        GhostEngineContext context = new GhostEngineContext("test-job", "test-spider", new Dictionary<string, object?>());

        // Act - Start the engine
        Task runTask = engine.RunAsync(spider, context);

        // Wait for first download to be in-progress (blocked on TaskCompletionSource)
        await WaitForConditionAsync(() => counter.InFlightCount > 0, TimeSpan.FromSeconds(5));

        // At this point, MaxInFlight should have been observed
        int observedMax = counter.MaxInFlightObserved;

        // Complete the download - this will allow the engine to continue
        downloadBlocker.SetResult(new GhostResponse(
            "http://example.com",
            200,
            new Dictionary<string, string>(),
            "<html></html>",
            DateTimeOffset.UtcNow));

        // Wait for engine to complete with timeout
        await runTask.WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        observedMax.Should().Be(1, "should never exceed MaxInFlight");
    }

    [Fact]
    public async Task RunAsync_WithMiddleware_ExecutesInCorrectOrderAsync()
    {
        // Arrange
        List<string> executionOrder = [];

        TestSpider spider = new TestSpider(new List<string>(), new List<string>());
        TestDownloader downloader = new TestDownloader();
        TestDownloaderMiddleware middleware1 = new TestDownloaderMiddleware("middleware1", executionOrder);
        TestDownloaderMiddleware middleware2 = new TestDownloaderMiddleware("middleware2", executionOrder);
        TestItemPipeline itemPipeline = new TestItemPipeline(new List<string>());

        GhostEngine engine = new GhostEngine(
            scheduler: new InMemoryRequestScheduler(),
            downloader: downloader,
            downloaderMiddlewares: new[] { middleware1, middleware2 },
            itemPipelines: new[] { itemPipeline });

        GhostEngineContext context = new GhostEngineContext("test-job", "test-spider", new Dictionary<string, object?>());

        // Act
        await engine.RunAsync(spider, context);

        // Assert
        // Middlewares should execute in reverse order of addition (last added = first to execute)
        executionOrder.Should().ContainInOrder("middleware2", "middleware1");
    }

    [Fact]
    public async Task RunAsync_WithCancellation_StopsGracefullyAsync()
    {
        // Arrange
        GhostEngineOptions options = new GhostEngineOptions
        {
            MaxInFlight = 10,
            MaxPendingItems = 10
        };

        CancellationTestSpider spider = new CancellationTestSpider();
        TestDownloader downloader = new TestDownloader();

        GhostEngine engine = new GhostEngine(
            options: options,
            scheduler: new InMemoryRequestScheduler(),
            downloader: downloader);

        GhostEngineContext context = new GhostEngineContext("test-job", "test-spider", new Dictionary<string, object?>());

        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        // Act & Assert
        // Cancellation can surface as OperationCanceledException or derived types.
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            engine.RunAsync(spider, context, cts.Token));
    }

    private static async Task WaitForConditionAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime start = DateTime.UtcNow;
        while (!condition())
        {
            if (DateTime.UtcNow - start > timeout)
            {
                throw new TimeoutException("Condition not met within timeout");
            }
            await Task.Yield();
        }
    }

    // Test helpers

    private sealed class TestSpider : ISpider
    {
        private readonly List<string> _processedRequests;
        private readonly List<string> _processedItems;

        public TestSpider(List<string> processedRequests, List<string> processedItems)
        {
            _processedRequests = processedRequests;
            _processedItems = processedItems;
        }

        public string Name => "TestSpider";

        public async IAsyncEnumerable<GhostRequest> StartRequestsAsync(GhostEngineContext context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            yield return new GhostRequest("http://example.com/1", "GET", new Dictionary<string, string>(), null, null);
            yield return new GhostRequest("http://example.com/2", "GET", new Dictionary<string, string>(), null, null);
            yield return new GhostRequest("http://example.com/3", "GET", new Dictionary<string, string>(), null, null);
        }

        public Task<SpiderOutput> ParseAsync(GhostResponse response, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            _processedRequests.Add(response.Url);
            ItemEnvelope item = new ItemEnvelope("test", new Dictionary<string, object?> { { "url", response.Url } }, DateTimeOffset.UtcNow);
            return Task.FromResult(new SpiderOutput(EmptyRequests, new[] { item }));
        }
    }

    private sealed class ControlledSpider : ISpider
    {
        private readonly int _requestCount;

        public ControlledSpider(int requestCount)
        {
            _requestCount = requestCount;
        }

        public string Name => "ControlledSpider";

        public async IAsyncEnumerable<GhostRequest> StartRequestsAsync(GhostEngineContext context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < _requestCount; i++)
            {
                yield return new GhostRequest($"http://example.com/{i}", "GET", new Dictionary<string, string>(), null, null);
            }
        }

        public Task<SpiderOutput> ParseAsync(GhostResponse response, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpiderOutput(EmptyRequests, EmptyItems));
        }
    }

    private sealed class CancellationTestSpider : ISpider
    {
        public string Name => "CancellationTestSpider";

        public async IAsyncEnumerable<GhostRequest> StartRequestsAsync(GhostEngineContext context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            int counter = 0;
            while (!cancellationToken.IsCancellationRequested)
            {
                yield return new GhostRequest($"http://example.com/{Interlocked.Increment(ref counter)}", "GET", new Dictionary<string, string>(), null, null);
                // Yield to allow cancellation to be processed
                await Task.Yield();
            }
        }

        public Task<SpiderOutput> ParseAsync(GhostResponse response, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new SpiderOutput(EmptyRequests, EmptyItems));
        }
    }

    private sealed class TestDownloader : IDownloader
    {
        public Task<GhostResponse> DownloadAsync(GhostRequest request, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new GhostResponse(
                request.Url,
                200,
                new Dictionary<string, string> { ["Content-Type"] = "text/html" },
                $"<html><body>Response for {request.Url}</body></html>",
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class InFlightCounter
    {
        public int InFlightCount;
        public int MaxInFlightObserved;
    }

    private sealed class SynchronousTestDownloader : IDownloader
    {
        private readonly InFlightCounter _counter;
        private readonly TaskCompletionSource<GhostResponse> _firstDownloadBlocker;
        private int _downloadCount;

        public SynchronousTestDownloader(InFlightCounter counter, TaskCompletionSource<GhostResponse> firstDownloadBlocker)
        {
            _counter = counter;
            _firstDownloadBlocker = firstDownloadBlocker;
        }

        public async Task<GhostResponse> DownloadAsync(GhostRequest request, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            int current = Interlocked.Increment(ref _counter.InFlightCount);
            int max = _counter.MaxInFlightObserved;
            if (current > max)
            {
                Interlocked.CompareExchange(ref _counter.MaxInFlightObserved, current, max);
            }

            int downloadNum = Interlocked.Increment(ref _downloadCount);

            try
            {
                if (downloadNum == 1)
                {
                    // First download blocks until signaled
                    return await _firstDownloadBlocker.Task.WaitAsync(cancellationToken);
                }
                else
                {
                    // Subsequent downloads complete immediately
                    return new GhostResponse(
                        request.Url,
                        200,
                        new Dictionary<string, string>(),
                        "<html></html>",
                        DateTimeOffset.UtcNow);
                }
            }
            finally
            {
                Interlocked.Decrement(ref _counter.InFlightCount);
            }
        }
    }

    private sealed class TestDownloaderMiddleware : IDownloaderMiddleware
    {
        private readonly string _name;
        private readonly List<string> _executionOrder;

        public TestDownloaderMiddleware(string name, List<string> executionOrder)
        {
            _name = name;
            _executionOrder = executionOrder;
        }

        public Task<GhostResponse> InvokeAsync(
            GhostRequest request,
            GhostEngineContext context,
            Func<GhostRequest, GhostEngineContext, CancellationToken, Task<GhostResponse>> nextStep,
            CancellationToken cancellationToken = default)
        {
            _executionOrder.Add(_name);
            return nextStep(request, context, cancellationToken);
        }
    }

    private sealed class TestItemPipeline : IItemPipeline
    {
        private readonly List<string> _processedItems;

        public TestItemPipeline(List<string> processedItems)
        {
            _processedItems = processedItems;
        }

        public Task<ItemEnvelope> ProcessAsync(ItemEnvelope item, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            _processedItems.Add(item.Data["url"]?.ToString() ?? "unknown");
            return Task.FromResult(item);
        }
    }
}
