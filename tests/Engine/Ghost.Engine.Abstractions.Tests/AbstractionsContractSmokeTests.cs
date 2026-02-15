using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Pipelines;
using Ghost.Engine.Abstractions.Scheduler;
using Ghost.Engine.Abstractions.Settings;
using Ghost.Engine.Abstractions.Signals;
using Ghost.Engine.Abstractions.Spider;
using Ghost.Engine.Abstractions.Transport;
using Xunit;

namespace Ghost.Engine.Abstractions.Tests;

public sealed class AbstractionsContractSmokeTests
{
    [Fact]
    public void CoreContracts_AreComposable()
    {
        var context = new GhostEngineContext("job-1", "spider-a", new Dictionary<string, object?>());
        var request = new GhostRequest("https://example.test", "GET", new Dictionary<string, string>(), null, TimeSpan.FromSeconds(10));
        var response = new GhostResponse("https://example.test", 200, new Dictionary<string, string>(), "ok", DateTimeOffset.UtcNow);
        var item = new ItemEnvelope("job", new Dictionary<string, object?> { ["id"] = "1" }, DateTimeOffset.UtcNow);
        var output = new SpiderOutput(new[] { request }, new[] { item });

        Assert.NotNull(context);
        Assert.NotNull(request);
        Assert.NotNull(response);
        Assert.NotNull(item);
        Assert.NotNull(output);
    }

    [Fact]
    public async Task Contracts_Compile_WithCancellationFlow()
    {
        CancellationToken cancellationToken = CancellationToken.None;
        var context = new GhostEngineContext("job-2", "spider-b", new Dictionary<string, object?>());
        var request = new GhostRequest("https://example.test", "GET", new Dictionary<string, string>(), null, TimeSpan.FromSeconds(5));
        var response = new GhostResponse("https://example.test", 200, new Dictionary<string, string>(), "body", DateTimeOffset.UtcNow);

        IGhostEngine engine = new FakeEngine();
        ISpider spider = new FakeSpider();
        IRequestScheduler scheduler = new FakeScheduler();
        IDownloader downloader = new FakeDownloader(response);
        IDownloaderMiddleware downloaderMiddleware = new FakeDownloaderMiddleware();
        ISpiderMiddleware spiderMiddleware = new FakeSpiderMiddleware();
        IItemPipeline itemPipeline = new FakePipeline();
        ISignalBus signalBus = new FakeSignalBus();
        IGhostSettings settings = new FakeSettings();

        await scheduler.EnqueueAsync(request, cancellationToken: cancellationToken);
        GhostRequest? dequeued = await scheduler.DequeueAsync(cancellationToken);
        GhostResponse downloaded = await downloader.DownloadAsync(dequeued!, context, cancellationToken);
        GhostResponse middlewareResponse = await downloaderMiddleware.InvokeAsync(
            dequeued!,
            context,
            (_, _, _) => Task.FromResult(downloaded),
            cancellationToken);
        SpiderOutput spiderOutput = await spiderMiddleware.InvokeAsync(
            middlewareResponse,
            context,
            (res, ctx, ct) => spider.ParseAsync(res, ctx, ct),
            cancellationToken);
        ItemEnvelope processedItem = await itemPipeline.ProcessAsync(spiderOutput.Items[0], context, cancellationToken);
        await signalBus.PublishAsync(processedItem, cancellationToken);
        await engine.RunAsync(spider, context, cancellationToken);

        Assert.NotNull(settings.GetOrDefault("none", "value"));
    }

    private sealed class FakeEngine : IGhostEngine
    {
        public static Task RunAsync(ISpider spider, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSpider : ISpider
    {
        public static string Name => "fake";

        public static async IAsyncEnumerable<GhostRequest> StartRequestsAsync(
            GhostEngineContext context,
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask.ConfigureAwait(false);
            yield return new GhostRequest("https://example.test", "GET", new Dictionary<string, string>(), null, null);
        }

        public Task<SpiderOutput> ParseAsync(GhostResponse response, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            var item = new ItemEnvelope("fake", new Dictionary<string, object?>(), DateTimeOffset.UtcNow);
            return Task.FromResult(new SpiderOutput(Array.Empty<GhostRequest>(), new[] { item }));
        }
    }

    private sealed class FakeScheduler : IRequestScheduler
    {
        private readonly Queue<GhostRequest> _queue = new();

        public ValueTask EnqueueAsync(GhostRequest request, int priority = 0, CancellationToken cancellationToken = default)
        {
            _queue.Enqueue(request);
            return ValueTask.CompletedTask;
        }

        public ValueTask<GhostRequest?> DequeueAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_queue.Count > 0 ? _queue.Dequeue() : null);
        }

        public ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(_queue.Count);
        }
    }

    private sealed class FakeDownloader(GhostResponse response) : IDownloader
    {
        public Task<GhostResponse> DownloadAsync(GhostRequest request, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(response);
        }
    }

    private sealed class FakeDownloaderMiddleware : IDownloaderMiddleware
    {
        public static Task<GhostResponse> InvokeAsync(
            GhostRequest request,
            GhostEngineContext context,
            Func<GhostRequest, GhostEngineContext, CancellationToken, Task<GhostResponse>> next,
            CancellationToken cancellationToken = default)
        {
            return next(request, context, cancellationToken);
        }
    }

    private sealed class FakeSpiderMiddleware : ISpiderMiddleware
    {
        public static Task<SpiderOutput> InvokeAsync(
            GhostResponse response,
            GhostEngineContext context,
            Func<GhostResponse, GhostEngineContext, CancellationToken, Task<SpiderOutput>> next,
            CancellationToken cancellationToken = default)
        {
            return next(response, context, cancellationToken);
        }
    }

    private sealed class FakePipeline : IItemPipeline
    {
        public Task<ItemEnvelope> ProcessAsync(ItemEnvelope item, GhostEngineContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(item);
        }
    }

    private sealed class FakeSignalBus : ISignalBus
    {
        public static Task PublishAsync<TSignal>(TSignal signal, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public ValueTask<ISignalSubscription> SubscribeAsync<TSignal>(Func<TSignal, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<ISignalSubscription>(new NullSubscription());
        }
    }

    private sealed class NullSubscription : ISignalSubscription
    {
        public static ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeSettings : IGhostSettings
    {
        public static bool TryGet<T>(string key, out T? value)
        {
            value = default;
            return false;
        }

        public static T GetOrDefault<T>(string key, T defaultValue) => defaultValue;
    }
}
