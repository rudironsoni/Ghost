using System.IO;
using Ghost.Internal;
using Ghost.Net;
using Ghost.Stealth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Ghost.Kernel;

public sealed class GhostKernel : IGhostKernel, IAsyncDisposable, IDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly SemaphoreSlim _sessionLock;
    private readonly bool _enableStealth;
    private readonly string _kernelBrowser;
    private readonly Socks5Bridge? _globalProxyBridge;
    private bool _disposed;

    private GhostKernel(IPlaywright playwright, IBrowser browser, int maxConcurrentSessions, bool enableStealth, string kernelBrowser, Socks5Bridge? globalProxyBridge = null)
    {
        ArgumentNullException.ThrowIfNull(playwright);
        ArgumentNullException.ThrowIfNull(browser);
        _playwright = playwright;
        _browser = browser;
        _sessionLock = new SemaphoreSlim(maxConcurrentSessions, maxConcurrentSessions);
        _enableStealth = enableStealth;
        _kernelBrowser = kernelBrowser ?? "Chromium";
        _globalProxyBridge = globalProxyBridge;

        // Ensure cleanup on process exit
        AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
    }

    public static async Task<GhostKernel> CreateAsync(KernelOptions? options = null, CancellationToken ct = default)
    {
        KernelOptions opts = options ?? new KernelOptions();

        var launchArgs = new List<string>(opts.Args ?? []);
        if (opts.EnableStealth && !opts.DisableDefaultStealthArgs)
        {
            // NOTE: Patchright automatically handles --disable-blink-features=AutomationControlled
            // No need to add this flag manually - it's patched at the binary level
            launchArgs.Add("--enable-quic");
            // Essential flags for server/container environments
            launchArgs.Add("--no-sandbox");
            launchArgs.Add("--disable-setuid-sandbox");
            launchArgs.Add("--disable-dev-shm-usage");
            // Disable GPU to avoid crashes - safer than swiftshader
            launchArgs.Add("--disable-gpu");
        }

        // Additional stealth flags that help prevent direct UDP leaks when using proxies
        if (opts.EnableStealth)
        {
            launchArgs.Add("--webrtc-ip-handling-policy=disable_non_proxied_udp");
            launchArgs.Add("--force-webrtc-ip-handling-policy=disable_non_proxied_udp");
            launchArgs.Add("--enforce-webrtc-ip-permission-check");
        }

        // Create Playwright instance and keep it alive
        IPlaywright playwright = await Playwright.CreateAsync().ConfigureAwait(false);

        Socks5Bridge? globalProxyBridge = null;
        Microsoft.Playwright.Proxy? browserProxy = null;

        try
        {
            // If a SOCKS5 proxy with authentication is provided at kernel level, create a global bridge
            if (opts.ProxyServer is not null && opts.ProxyServer.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase))
            {
                string? proxyUsername = Environment.GetEnvironmentVariable("DOTNET_GHOST_PROXY_USERNAME");
                string? proxyPassword = Environment.GetEnvironmentVariable("DOTNET_GHOST_PROXY_PASSWORD");

                if (!string.IsNullOrEmpty(proxyUsername) && !string.IsNullOrEmpty(proxyPassword))
                {
                    var uri = new Uri(opts.ProxyServer);
                    globalProxyBridge = new Socks5Bridge(uri.Host, uri.Port, proxyUsername, proxyPassword);
                    globalProxyBridge.Start();

                    // Use the bridge as browser-level proxy
                    browserProxy = new Microsoft.Playwright.Proxy
                    {
                        Server = $"socks5://127.0.0.1:{globalProxyBridge.Port}"
                    };

                    // Add Chromium arg to ensure SOCKS5 DNS resolution works correctly
                    launchArgs.Add("--host-resolver-rules=MAP * ~NOTFOUND , EXCLUDE 127.0.0.1");
                }
                else
                {
                    // No credentials, use proxy directly
                    browserProxy = new Microsoft.Playwright.Proxy { Server = opts.ProxyServer };
                }
            }
            else if (opts.ProxyServer is not null)
            {
                browserProxy = new Microsoft.Playwright.Proxy { Server = opts.ProxyServer };
            }

            IBrowser browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = opts.Headless,
                SlowMo = opts.SlowMo,
                Proxy = browserProxy,
                Args = launchArgs
            }).ConfigureAwait(false);

            return new GhostKernel(playwright, browser, opts.MaxConcurrentSessions, opts.EnableStealth, opts.Browser, globalProxyBridge);
        }
        catch
        {
            globalProxyBridge?.Dispose();
            playwright.Dispose();
            throw;
        }
    }

    public async Task<IBrowserSession> NewSessionAsync(SessionOptions? options = null, CancellationToken ct = default)
    {
        await _sessionLock.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            FingerprintProfile? profile = null;
            if (_enableStealth)
            {
                profile = FingerprintGenerator.Generate();
            }

            // Only set StorageStatePath when provided and the file actually exists.
            // If a path was provided but the file is missing, ignore it so Playwright
            // doesn't crash with a "file does not exist" error.
            string? storageStatePath = options?.StorageStatePath;
            if (!string.IsNullOrEmpty(storageStatePath) && !File.Exists(storageStatePath))
            {
                // Provided path doesn't exist - ignore and start a fresh session.
                storageStatePath = null;
            }

            var ctxOptions = new BrowserNewContextOptions
            {
                ViewportSize = new ViewportSize
                {
                    Width = options?.ViewportWidth ?? profile?.ViewportWidth ?? 1280,
                    Height = options?.ViewportHeight ?? profile?.ViewportHeight ?? 720
                },
                UserAgent = options?.UserAgent ?? profile?.UserAgent,
                StorageStatePath = storageStatePath,
                TimezoneId = options?.TimezoneId ?? profile?.TimeZone ?? "UTC",
                Locale = options?.Locale ?? "en-US"
            };

            SessionOptions.ProxySettings? proxy = options?.Proxy;

            // If there's a global browser-level SOCKS5 proxy bridge, don't override with session-level proxy
            // Session-level proxies only work for non-SOCKS5 or when no browser-level proxy is set
            if (proxy is not null && _globalProxyBridge == null)
            {
                // Context-level proxy settings (for HTTP/HTTPS or SOCKS5 without auth)
                // Note: SOCKS5 with authentication doesn't work reliably at context level in Chromium
                if (proxy.Server is not null && proxy.Server.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(proxy.Username))
                {
                    // SOCKS5 with auth requires a bridge, but context-level bridges are unreliable
                    // Recommend using browser-level proxy via KernelOptions.ProxyServer instead
                    throw new NotSupportedException(
                        "SOCKS5 proxies with authentication at the session level are not supported. " +
                        "Please configure the SOCKS5 proxy at the kernel level using KernelOptions.ProxyServer " +
                        "and set DOTNET_GHOST_PROXY_USERNAME and DOTNET_GHOST_PROXY_PASSWORD environment variables.");
                }
                else
                {
                    // HTTP/HTTPS or SOCKS5 without auth - can use context-level proxy
                    ctxOptions.Proxy = new Microsoft.Playwright.Proxy
                    {
                        Server = proxy.Server!,
                        Username = proxy.Username,
                        Password = proxy.Password,
                        Bypass = proxy.Bypass
                    };
                }
            }

            if (options?.Geolocation is not null)
            {
                ctxOptions.Geolocation = new Microsoft.Playwright.Geolocation
                {
                    Latitude = (float)options.Geolocation.Latitude,
                    Longitude = (float)options.Geolocation.Longitude,
                    Accuracy = (float)options.Geolocation.Accuracy
                };

                List<string> perms = ctxOptions.Permissions?.ToList() ?? new List<string>();
                if (!perms.Contains("geolocation")) perms.Add("geolocation");
                ctxOptions.Permissions = perms;
            }
            else if (profile is not null)
            {
                ctxOptions.Geolocation = new Microsoft.Playwright.Geolocation
                {
                    Latitude = (float)profile.Latitude,
                    Longitude = (float)profile.Longitude,
                    Accuracy = 50
                };

                List<string> perms = ctxOptions.Permissions?.ToList() ?? new List<string>();
                if (!perms.Contains("geolocation")) perms.Add("geolocation");
                ctxOptions.Permissions = perms;
            }
            else if (profile is not null)
            {
                // Use profile geolocation if not overridden
                ctxOptions.Geolocation = new Microsoft.Playwright.Geolocation
                {
                    Latitude = (float)profile.Latitude,
                    Longitude = (float)profile.Longitude,
                    Accuracy = 50f
                };
                // Ensure permission
                List<string> _perms = ctxOptions.Permissions?.ToList() ?? new List<string>();
                if (!_perms.Contains("geolocation")) _perms.Add("geolocation");
                ctxOptions.Permissions = _perms;
            }

            // Ensure geolocation permission if set
            if (ctxOptions.Geolocation is not null)
            {
                List<string> _perms2 = ctxOptions.Permissions?.ToList() ?? new List<string>();
                if (!_perms2.Contains("geolocation")) _perms2.Add("geolocation");
                ctxOptions.Permissions = _perms2;
            }

            if (options?.Permissions is not null && options.Permissions.Count > 0)
            {
                List<string> _perms3 = ctxOptions.Permissions?.ToList() ?? new List<string>();
                foreach (string p in options.Permissions)
                {
                    if (!_perms3.Contains(p)) _perms3.Add(p);
                }
                ctxOptions.Permissions = _perms3;
            }

            IBrowserContext context = await _browser.NewContextAsync(ctxOptions).ConfigureAwait(false);

            // Inject Stealth Scripts
            if (_enableStealth && profile is not null)
            {
                string script = StealthScripts.GetInitScript(profile);
                await context.AddInitScriptAsync(script).ConfigureAwait(false);
            }

            string sessionId = Guid.NewGuid().ToString();
            return new BrowserSessionWrapper(context, sessionId, () => _sessionLock.Release(), null);
        }
        catch
        {
            _sessionLock.Release();
            throw;
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        try
        {
            await _browser.CloseAsync().ConfigureAwait(false);
        }
        catch { }

        try
        {
            await _browser.DisposeAsync().ConfigureAwait(false);
        }
        catch { }

        try
        {
            _playwright.Dispose();
        }
        catch { }

        try
        {
            _globalProxyBridge?.Dispose();
        }
        catch { }

        try
        {
            _sessionLock.Dispose();
        }
        catch { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await DisposeAsyncCore().ConfigureAwait(false);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_disposed) return;
        // Note: We cannot call async cleanup synchronously here.
        // Callers should use DisposeAsync for proper cleanup.
        // If synchronous disposal is required, resources will be cleaned up by finalizer or process exit.
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
