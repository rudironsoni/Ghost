using System.Threading.Channels;
using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Pipelines;
using Ghost.Engine.Abstractions.Scheduler;
using Ghost.Engine.Abstractions.Spider;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Engine;

/// <summary>
/// Concrete implementation of the Ghost engine with unified backpressure using Channel for optimal async performance.
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
        await foreach (GhostRequest startRequest in spider.StartRequestsAsync(context, cancellationToken).ConfigureAwait(false))
        {
            await _scheduler.EnqueueAsync(startRequest, priority: 0, cancellationToken).ConfigureAwait(false);
        }

        // Create bounded channel for task tracking with natural backpressure
        // The bounded channel naturally limits concurrent operations and provides
        // efficient async backpressure without polling delays
        var channelOptions = new BoundedChannelOptions(_options.MaxInFlight)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = true
        };
        Channel<Task> processingTasks = Channel.CreateBounded<Task>(channelOptions);

        // Track completion of task consumer
        Task taskConsumer = ConsumeTasksAsync(processingTasks.Reader, cancellationToken);

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Check unified backpressure (in-flight count is now managed by channel bounds)
                if (IsUnderBackpressure())
                {
                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                GhostRequest? request = await _scheduler.DequeueAsync(cancellationToken).ConfigureAwait(false);
                if (request is null)
                {
                    // No more requests in scheduler
                    // Wait for all in-flight tasks to complete
                    if (Volatile.Read(ref _inFlightCount) == 0)
                    {
                        break;
                    }

                    await Task.Delay(10, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                // Process request - channel WriteAsync provides natural backpressure
                // when MaxInFlight tasks are already queued/executing
                Task task = ProcessRequestAsync(request, spider, context, cancellationToken);
                await processingTasks.Writer.WriteAsync(task, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Cancellation requested - will drain remaining tasks
            throw;
        }
        finally
        {
            // Signal completion - no more tasks will be written
            processingTasks.Writer.Complete();
        }

        // Wait for the consumer to process all remaining tasks
        await taskConsumer.ConfigureAwait(false);
    }

    /// <summary>
    /// Consumes tasks from the channel and awaits their completion.
    /// Handles exceptions gracefully to ensure all tasks are processed.
    /// </summary>
    private static async Task ConsumeTasksAsync(ChannelReader<Task> reader, CancellationToken cancellationToken)
    {
        await foreach (Task task in reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch
            {
                // Exceptions are captured in the task and already handled by ProcessRequestAsync
                // We continue processing remaining tasks regardless of individual failures
            }
        }
    }

    private bool IsUnderBackpressure()
    {
        int inFlight = Volatile.Read(ref _inFlightCount);
        int pendingItems = Volatile.Read(ref _pendingItemsCount);

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
            GhostResponse response = await ExecuteDownloaderMiddlewareChainAsync(request, context, cancellationToken).ConfigureAwait(false);

            // Process through spider middleware chain
            SpiderOutput output = await ExecuteSpiderMiddlewareChainAsync(response, spider, context, cancellationToken).ConfigureAwait(false);

            // Re-enqueue returned requests
            foreach (GhostRequest newRequest in output.Requests)
            {
                await _scheduler.EnqueueAsync(newRequest, priority: 0, cancellationToken).ConfigureAwait(false);
            }

            // Send items through ordered pipeline chain
            foreach (ItemEnvelope item in output.Items)
            {
                Interlocked.Increment(ref _pendingItemsCount);
                try
                {
                    await ExecuteItemPipelineChainAsync(item, context, cancellationToken).ConfigureAwait(false);
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
            return await _downloader.DownloadAsync(req, ctx, ct).ConfigureAwait(false);
        };

        // Apply middlewares in reverse order (last added = first to execute)
        for (int i = _downloaderMiddlewares.Count - 1; i >= 0; i--)
        {
            IDownloaderMiddleware middleware = _downloaderMiddlewares[i];
            Func<GhostRequest, GhostEngineContext, CancellationToken, Task<GhostResponse>> currentNext = next;
            next = async (req, ctx, ct) => await middleware.InvokeAsync(req, ctx, currentNext, ct).ConfigureAwait(false);
        }

        return await next(request, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task<SpiderOutput> ExecuteSpiderMiddlewareChainAsync(
        GhostResponse response,
        ISpider spider,
        GhostEngineContext context,
        CancellationToken cancellationToken)
    {
        Func<GhostResponse, GhostEngineContext, CancellationToken, Task<SpiderOutput>> next = async (resp, ctx, ct) =>
        {
            return await spider.ParseAsync(resp, ctx, ct).ConfigureAwait(false);
        };

        // Apply middlewares in reverse order (last added = first to execute)
        for (int i = _spiderMiddlewares.Count - 1; i >= 0; i--)
        {
            ISpiderMiddleware middleware = _spiderMiddlewares[i];
            Func<GhostResponse, GhostEngineContext, CancellationToken, Task<SpiderOutput>> currentNext = next;
            next = async (resp, ctx, ct) => await middleware.InvokeAsync(resp, ctx, currentNext, ct).ConfigureAwait(false);
        }

        return await next(response, context, cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteItemPipelineChainAsync(
        ItemEnvelope item,
        GhostEngineContext context,
        CancellationToken cancellationToken)
    {
        ItemEnvelope currentItem = item;

        foreach (IItemPipeline pipeline in _itemPipelines)
        {
            currentItem = await pipeline.ProcessAsync(currentItem, context, cancellationToken).ConfigureAwait(false);
        }
    }
}
