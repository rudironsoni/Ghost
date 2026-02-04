using Ghost.Sdk.Spider.Adapters.Contracts;
using GhostExecutionContext = Ghost.Sdk.Spider.Engine.ExecutionContext;
using SpiderBase = Ghost.Sdk.Spider.Engine.Spider;

namespace Ghost.Sdk.Spider.Tests.TestHelpers;

/// <summary>
/// Simple test spider for unit testing
/// </summary>
public class TestSpider : SpiderBase
{
    private readonly List<string> _startUrls;
    private readonly Action<Response, GhostExecutionContext>? _processCallback;

    public override string Name => "TestSpider";

    public List<Response> ProcessedResponses { get; } = new();
    public List<Exception> Errors { get; } = new();

    public TestSpider(List<string>? startUrls = null, Action<Response, GhostExecutionContext>? processCallback = null)
    {
        _startUrls = startUrls ?? new List<string> { "https://example.com" };
        _processCallback = processCallback;
    }

    public override IEnumerable<string> GetStartUrls() => _startUrls;

    public override Task ProcessResponseAsync(Response response, GhostExecutionContext context, CancellationToken cancellationToken = default)
    {
        ProcessedResponses.Add(response);
        _processCallback?.Invoke(response, context);
        return Task.CompletedTask;
    }

    public override Task OnErrorAsync(Exception exception, GhostExecutionContext context, CancellationToken cancellationToken = default)
    {
        Errors.Add(exception);
        return base.OnErrorAsync(exception, context, cancellationToken);
    }
}

/// <summary>
/// Configurable test spider for advanced testing scenarios
/// </summary>
public class ConfigurableTestSpider : SpiderBase
{
    public override string Name { get; }
    public override Engine.SpiderOptions Options { get; }

    public Func<IEnumerable<string>>? GetStartUrlsFunc { get; set; }
    public Func<Response, GhostExecutionContext, CancellationToken, Task>? ProcessFunc { get; set; }
    public Func<string, GhostExecutionContext, bool>? ShouldFollowFunc { get; set; }

    public bool OnStartCalled { get; private set; }
    public bool OnCompleteCalled { get; private set; }
    public List<Exception> ErrorsReceived { get; } = new();

    public ConfigurableTestSpider(string name = "ConfigurableTestSpider", Engine.SpiderOptions? options = null)
    {
        Name = name;
        Options = options ?? new Engine.SpiderOptions();
    }

    public override IEnumerable<string> GetStartUrls()
    {
        return GetStartUrlsFunc?.Invoke() ?? new[] { "https://test.com" };
    }

    public override Task ProcessResponseAsync(Response response, GhostExecutionContext context, CancellationToken cancellationToken = default)
    {
        return ProcessFunc?.Invoke(response, context, cancellationToken) ?? Task.CompletedTask;
    }

    public override Task OnStartAsync(GhostExecutionContext context, CancellationToken cancellationToken = default)
    {
        OnStartCalled = true;
        return base.OnStartAsync(context, cancellationToken);
    }

    public override Task OnCompleteAsync(GhostExecutionContext context, Engine.SpiderResult result, CancellationToken cancellationToken = default)
    {
        OnCompleteCalled = true;
        return base.OnCompleteAsync(context, result, cancellationToken);
    }

    public override Task OnErrorAsync(Exception exception, GhostExecutionContext context, CancellationToken cancellationToken = default)
    {
        ErrorsReceived.Add(exception);
        return base.OnErrorAsync(exception, context, cancellationToken);
    }

    public override bool ShouldFollowUrl(string url, GhostExecutionContext context)
    {
        return ShouldFollowFunc?.Invoke(url, context) ?? base.ShouldFollowUrl(url, context);
    }
}
