using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Patchright;
using Ghost.Internal;
namespace Ghost.Core;

public sealed class GhostwriterKernel : IAsyncDisposable
{
    private readonly IBrowser _browser;
    private readonly SemaphoreSlim _sessionLock;
    private bool _disposed;

    private GhostwriterKernel(IBrowser browser, int maxConcurrentSessions)
    {
        ArgumentNullException.ThrowIfNull(browser);
        _browser = browser;
        _sessionLock = new SemaphoreSlim(maxConcurrentSessions, maxConcurrentSessions);
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

        return new GhostwriterKernel(browser, opts.MaxConcurrentSessions);
    }

    public async ValueTask<IBrowserSession> NewSessionAsync(SessionOptions? options = null, CancellationToken ct = default)
    {
        await _sessionLock.WaitAsync(ct);

        try
        {
            var ctxOptions = new BrowserNewContextOptions
            {
                ViewportWidth = options?.ViewportWidth ?? 1280,
                ViewportHeight = options?.ViewportHeight ?? 720,
                UserAgent = options?.UserAgent
            };

            if (options?.Proxy is not null)
            {
                ctxOptions.Proxy = new Patchright.Proxy
                {
                    Server = options.Proxy.Server,
                    Username = options.Proxy.Username,
                    Password = options.Proxy.Password,
                    Bypass = options.Proxy.Bypass
                };
                // Note: Patchright.Proxy in LaunchOptions only has Server in stubs; include credentials if supported by real Patchright
            }

            if (options?.Geolocation is not null)
            {
                ctxOptions.Geolocation = new PlaywrightGeolocation { Latitude = options.Geolocation.Latitude, Longitude = options.Geolocation.Longitude, Accuracy = options.Geolocation.Accuracy };
                // ensure geolocation permission
                ctxOptions.Permissions ??= new List<string>();
                if (!ctxOptions.Permissions.Contains("geolocation")) ctxOptions.Permissions.Add("geolocation");
            }

            if (options?.Permissions is not null && options.Permissions.Count > 0)
            {
                ctxOptions.Permissions ??= new List<string>();
                foreach (var p in options.Permissions)
                {
                    if (!ctxOptions.Permissions.Contains(p)) ctxOptions.Permissions.Add(p);
                }
            }

            var context = await _browser.NewContextAsync(ctxOptions, ct);

            var sessionId = Guid.NewGuid().ToString();
            return new BrowserSessionWrapper(context, sessionId, () => _sessionLock.Release());
        }
        catch
        {
            _sessionLock.Release();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _browser.DisposeAsync();
        _sessionLock.Dispose();
        _disposed = true;
    }
}
