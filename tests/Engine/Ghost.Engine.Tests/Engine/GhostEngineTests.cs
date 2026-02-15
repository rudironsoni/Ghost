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
    public async Task RunAsync_WithFakeDownloader_ProcessesRequestsDeterministically()
    {
        // Arrange
        var options = new GhostEngineOptions
        {
            MaxInFlight = 2,
            MaxPendingItems = 10
        };

        var processedRequests = new List<string>();
        var processedItems = new List<string>();

        var spider = new TestSpider(processedRequests, processedItems);
        var downloader = new TestDownloader();
        var itemPipeline = new TestItemPipeline(processedItems);

        var engine = new GhostEngine(
            options: options,
            scheduler: new InMemoryRequestScheduler(),
            downloader: downloader,
            downloaderMiddlewares: Array.Empty<IDownloaderMiddleware>(),
            spiderMiddlewares: Array.Empty<ISpiderMiddleware>(),
            itemPipelines: new[] { itemPipeline });

        var context = new GhostEngineContext("test-job", "test-spider", new Dictionary<string, object?>());

        // Act
        await engine.RunAsync(spider, context);

        // Assert
        processedRequests.Should().HaveCount(3, "should process 3 start requests");
        processedRequests.Should().BeEquivalentTo(TestUrls,
            options => options.WithStrictOrdering());

        processedItems.Should().HaveCount(3, "should process 3 items");
    }

    [Fact]
    public async Task RunAsync_WithBackpressure_RespectsMaxInFlight()
    {
        // Arrange
        var options = new GhostEngineOptions
        {
            MaxInFlight = 1,
            MaxPendingItems = 10
        };

        var counter = new InFlightCounter();

        var spider = new SlowSpider();
        var downloader = new CountingDownloader(counter);

        var engine = new GhostEngine(
            options: options,
            scheduler: new InMemoryRequestScheduler(),
            downloader: downloader);

        var context = new GhostEngineContext("test-job", "test-spider", new Dictionary<string, object?>());

        // Act
        await engine.RunAsync(spider, context);

        // Assert
        counter.MaxInFlightObserved.Should().Be(1, "should never exceed MaxInFlight");
    }

    [Fact]
    public async Task RunAsync_WithMiddleware_ExecutesInCorrectOrder()
    {
        // Arrange
        var executionOrder = new List<string>();

        var spider = new TestSpider(new List<string>(), new List<string>());
        var downloader = new TestDownloader();
        var middleware1 = new TestDownloaderMiddleware("middleware1", executionOrder);
        var middleware2 = new TestDownloaderMiddleware("middleware2", executionOrder);
        var itemPipeline = new TestItemPipeline(new List<string>());

        var engine = new GhostEngine(
            scheduler: new InMemoryRequestScheduler(),
            downloader: downloader,
            downloaderMiddlewares: new[] { middleware1, middleware2 },
            itemPipelines: new[] { itemPipeline });

        var context = new GhostEngineContext("test-job", "test-spider", new Dictionary<string, object?>());

        // Act
        await engine.RunAsync(spider, context);

        // Assert
        // Middlewares should execute in reverse order of addition (last added = first to execute)
        executionOrder.Should().ContainInOrder("middleware2", "middleware1");
    }

    [Fact]
    public async Task RunAsync_WithCancellation_StopsGracefully()
    {
        // Arrange
        var options = new GhostEngineOptions
        {
            MaxInFlight = 10,
            MaxPendingItems = 10
        };

        var spider = new InfiniteSpider();
        var downloader = new TestDownloader();

        var engine = new GhostEngine(
            options: options,
            scheduler: new InMemoryRequestScheduler(),
            downloader: downloader);

        var context = new GhostEngineContext("test-job", "test-spider", new Dictionary<string, object?>());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        // Act & Assert
        // Should throw TaskCanceledException when cancelled
        await Assert.ThrowsAsync<TaskCanceledException>(() =>
            engine.RunAsync(spider, context, cts.Token));
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
            var item = new ItemEnvelope("test", new Dictionary<string, object?> { { "url", response.Url } }, DateTimeOffset.UtcNow);
            return Task.FromResult(new SpiderOutput(EmptyRequests, new[] { item }));
        }
    }

    private sealed class SlowSpider : ISpider
    {
        public string Name => "SlowSpider";

        public async IAsyncEnumerable<GhostRequest> StartRequestsAsync(GhostEngineContext context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                yield return new GhostRequest($"http://example.com/{i}", "GET", new Dictionary<string, string>(), null, null);
            }
        }

        public async Task<SpiderOutput> ParseAsync(GhostResponse response, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            await Task.Delay(50, cancellationToken).ConfigureAwait(false); // Simulate slow processing
            return new SpiderOutput(EmptyRequests, EmptyItems);
        }
    }

    private sealed class InfiniteSpider : ISpider
    {
        private int _counter;

        public string Name => "InfiniteSpider";

        public async IAsyncEnumerable<GhostRequest> StartRequestsAsync(GhostEngineContext context, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                yield return new GhostRequest($"http://example.com/{Interlocked.Increment(ref _counter)}", "GET", new Dictionary<string, string>(), null, null);
                await Task.Delay(10, cancellationToken).ConfigureAwait(false);
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

    private sealed class CountingDownloader : IDownloader
    {
        private readonly InFlightCounter _counter;

        public CountingDownloader(InFlightCounter counter)
        {
            _counter = counter;
        }

        public async Task<GhostResponse> DownloadAsync(GhostRequest request, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            int current = Interlocked.Increment(ref _counter.InFlightCount);
            int max = _counter.MaxInFlightObserved;
            if (current > max)
            {
                Interlocked.CompareExchange(ref _counter.MaxInFlightObserved, current, max);
            }

            await Task.Delay(50, cancellationToken).ConfigureAwait(false);

            Interlocked.Decrement(ref _counter.InFlightCount);

            return new GhostResponse(
                request.Url,
                200,
                new Dictionary<string, string>(),
                "<html></html>",
                DateTimeOffset.UtcNow);
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
