using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Patchright;
using Ghostwright.Internal;
namespace Ghostwright.Core;

public sealed class GhostwriterKernel : IAsyncDisposable
{
    private readonly IBrowser _browser;
    private bool _disposed;

    private GhostwriterKernel(IBrowser browser)
    {
        ArgumentNullException.ThrowIfNull(browser);
        _browser = browser;
    }

    public static async Task<GhostwriterKernel> CreateAsync(KernelOptions? options = null, CancellationToken ct = default)
    {
        var opts = options ?? new KernelOptions();
        var browser = await Patchright.Patchright.LaunchAsync(new LaunchOptions
        {
            Headless = opts.Headless,
            SlowMo = opts.SlowMo,
            Proxy = opts.ProxyServer is not null ? new Proxy { Server = opts.ProxyServer } : null
        }, ct);

        return new GhostwriterKernel(browser);
    }

    public async ValueTask<IBrowserSession> NewSessionAsync(SessionOptions? options = null, CancellationToken ct = default)
    {
        var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportWidth = options?.ViewportWidth ?? 1280,
            ViewportHeight = options?.ViewportHeight ?? 720,
            UserAgent = options?.UserAgent
        }, ct);

        var sessionId = Guid.NewGuid().ToString();
        return new BrowserSessionWrapper(context, sessionId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _browser.DisposeAsync();
        _disposed = true;
    }
}
