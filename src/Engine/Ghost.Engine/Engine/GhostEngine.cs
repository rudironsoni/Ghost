using System.Collections.Concurrent;
using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Pipelines;
using Ghost.Engine.Abstractions.Scheduler;
using Ghost.Engine.Abstractions.Spider;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Engine;

/// <summary>
/// Concrete implementation of the Ghost engine with unified backpressure.
/// </summary>
public sealed class GhostEngine : IGhostEngine
{
    private readonly GhostEngineOptions _options;
    private readonly IRequestScheduler _scheduler;
    private readonly IDownloader _downloader;
    private readonly IReadOnlyList<IDownloaderMiddleware> _downloaderMiddlewares;
    private readonly IReadOnlyList<ISpiderMiddleware> _spiderMiddlewares;
    private readonly IReadOnlyList<IItemPipeline> _itemPipelines;

    private int _inFlightCount;
    private int _pendingItemsCount;

    public GhostEngine(
        GhostEngineOptions? options = null,
        IRequestScheduler? scheduler = null,
        IDownloader? downloader = null,
        IReadOnlyList<IDownloaderMiddleware>? downloaderMiddlewares = null,
        IReadOnlyList<ISpiderMiddleware>? spiderMiddlewares = null,
        IReadOnlyList<IItemPipeline>? itemPipelines = null)
    {
        _options = options ?? new GhostEngineOptions();
        _scheduler = scheduler ?? new Scheduler.InMemoryRequestScheduler();
        _downloader = downloader ?? new Downloader.FakeDownloader();
        _downloaderMiddlewares = downloaderMiddlewares ?? Array.Empty<IDownloaderMiddleware>();
        _spiderMiddlewares = spiderMiddlewares ?? Array.Empty<ISpiderMiddleware>();
        _itemPipelines = itemPipelines ?? Array.Empty<IItemPipeline>();
    }

    public async Task RunAsync(ISpider spider, GhostEngineContext context, CancellationToken cancellationToken = default)
    {
        // Consume start requests into scheduler
        await foreach (var startRequest in spider.StartRequestsAsync(context, cancellationToken))
        {
            await _scheduler.EnqueueAsync(startRequest, priority: 0, cancellationToken);
        }

        // Process requests until scheduler is empty and no in-flight requests
        var processingTasks = new ConcurrentBag<Task>();

        while (!cancellationToken.IsCancellationRequested)
        {
            // Check unified backpressure
            if (IsUnderBackpressure())
            {
                await Task.Delay(10, cancellationToken);
                continue;
            }

            var request = await _scheduler.DequeueAsync(cancellationToken);
            if (request == null)
            {
                // No more requests and no in-flight work
                if (Volatile.Read(ref _inFlightCount) == 0)
                {
                    break;
                }
                await Task.Delay(10, cancellationToken);
                continue;
            }

            // Process request
            var task = ProcessRequestAsync(request, spider, context, cancellationToken);
            processingTasks.Add(task);
        }

        // Wait for all processing to complete
        await Task.WhenAll(processingTasks);
    }

    private bool IsUnderBackpressure()
    {
        var inFlight = Volatile.Read(ref _inFlightCount);
        var pendingItems = Volatile.Read(ref _pendingItemsCount);

        return inFlight >= _options.MaxInFlight || pendingItems >= _options.MaxPendingItems;
    }

    private async Task ProcessRequestAsync(
        GhostRequest request,
        ISpider spider,
        GhostEngineContext context,
        CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _inFlightCount);
        try
        {
            // Process through downloader middleware chain
            var response = await ExecuteDownloaderMiddlewareChainAsync(request, context, cancellationToken);

            // Process through spider middleware chain
            var output = await ExecuteSpiderMiddlewareChainAsync(response, spider, context, cancellationToken);

            // Re-enqueue returned requests
            foreach (var newRequest in output.Requests)
            {
                await _scheduler.EnqueueAsync(newRequest, priority: 0, cancellationToken);
            }

            // Send items through ordered pipeline chain
            foreach (var item in output.Items)
            {
                Interlocked.Increment(ref _pendingItemsCount);
                try
                {
                    await ExecuteItemPipelineChainAsync(item, context, cancellationToken);
                }
                finally
                {
                    Interlocked.Decrement(ref _pendingItemsCount);
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _inFlightCount);
        }
    }

    private async Task<GhostResponse> ExecuteDownloaderMiddlewareChainAsync(
        GhostRequest request,
        GhostEngineContext context,
        CancellationToken cancellationToken)
    {
        Func<GhostRequest, GhostEngineContext, CancellationToken, Task<GhostResponse>> next = async (req, ctx, ct) =>
        {
            return await _downloader.DownloadAsync(req, ctx, ct);
        };

        // Apply middlewares in reverse order (last added = first to execute)
        for (int i = _downloaderMiddlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _downloaderMiddlewares[i];
            var currentNext = next;
            next = async (req, ctx, ct) => await middleware.InvokeAsync(req, ctx, currentNext, ct);
        }

        return await next(request, context, cancellationToken);
    }

    private async Task<SpiderOutput> ExecuteSpiderMiddlewareChainAsync(
        GhostResponse response,
        ISpider spider,
        GhostEngineContext context,
        CancellationToken cancellationToken)
    {
        Func<GhostResponse, GhostEngineContext, CancellationToken, Task<SpiderOutput>> next = async (resp, ctx, ct) =>
        {
            return await spider.ParseAsync(resp, ctx, ct);
        };

        // Apply middlewares in reverse order (last added = first to execute)
        for (int i = _spiderMiddlewares.Count - 1; i >= 0; i--)
        {
            var middleware = _spiderMiddlewares[i];
            var currentNext = next;
            next = async (resp, ctx, ct) => await middleware.InvokeAsync(resp, ctx, currentNext, ct);
        }

        return await next(response, context, cancellationToken);
    }

    private async Task ExecuteItemPipelineChainAsync(
        ItemEnvelope item,
        GhostEngineContext context,
        CancellationToken cancellationToken)
    {
        var currentItem = item;

        foreach (var pipeline in _itemPipelines)
        {
            currentItem = await pipeline.ProcessAsync(currentItem, context, cancellationToken);
        }
    }
}
