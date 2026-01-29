using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
using Ghost.Net;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Ghost.Internal;
using Ghost.Stealth;

namespace Ghost.Core;

public sealed class GhostKernel : IAsyncDisposable, IDisposable
{
    private readonly IPlaywright _playwright;
    private readonly IBrowser _browser;
    private readonly SemaphoreSlim _sessionLock;
    private readonly bool _enableStealth;
    private readonly string _kernelBrowser;
    private bool _disposed;

    private GhostKernel(IPlaywright playwright, IBrowser browser, int maxConcurrentSessions, bool enableStealth, string kernelBrowser)
    {
        ArgumentNullException.ThrowIfNull(playwright);
        ArgumentNullException.ThrowIfNull(browser);
        _playwright = playwright;
        _browser = browser;
        _sessionLock = new SemaphoreSlim(maxConcurrentSessions, maxConcurrentSessions);
        _enableStealth = enableStealth;
        _kernelBrowser = kernelBrowser ?? "Chromium";
        
        // Ensure cleanup on process exit
        AppDomain.CurrentDomain.ProcessExit += (s, e) => Dispose();
    }

    public static async Task<GhostKernel> CreateAsync(KernelOptions? options = null, CancellationToken ct = default)
    {
        var opts = options ?? new KernelOptions();

        var launchArgs = new List<string>(opts.Args ?? []);
        if (opts.EnableStealth && !opts.DisableDefaultStealthArgs)
        {
            launchArgs.Add("--disable-blink-features=AutomationControlled");
            launchArgs.Add("--enable-quic");
            launchArgs.Add("--use-gl=desktop");
            launchArgs.Add("--no-sandbox");
        }

        // Additional stealth flags that help prevent direct UDP leaks when using proxies
        if (opts.EnableStealth)
        {
            launchArgs.Add("--webrtc-ip-handling-policy=disable_non_proxied_udp");
            launchArgs.Add("--force-webrtc-ip-handling-policy=disable_non_proxied_udp");
            launchArgs.Add("--enforce-webrtc-ip-permission-check");
        }

        // Create Playwright instance and keep it alive
        var playwright = await Microsoft.Playwright.Playwright.CreateAsync();

        try
        {
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = opts.Headless,
                SlowMo = opts.SlowMo,
                Proxy = opts.ProxyServer is not null ? new Microsoft.Playwright.Proxy { Server = opts.ProxyServer } : null,
                Args = launchArgs
            });

            return new GhostKernel(playwright, browser, opts.MaxConcurrentSessions, opts.EnableStealth, opts.Browser);
        }
        catch
        {
            playwright.Dispose();
            throw;
        }
    }

    public async Task<IBrowserSession> NewSessionAsync(SessionOptions? options = null, CancellationToken ct = default)
    {
        await _sessionLock.WaitAsync(ct);

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

            IAsyncDisposable? bridgeAdapter = null;
            Socks5Bridge? bridge = null;

            var proxy = options?.Proxy;
            if (proxy is not null)
            {
                // If upstream is SOCKS5 with username/password and running Chromium, create a local bridge
                if (proxy.Server is not null && proxy.Server.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(proxy.Username) &&
                    string.Equals(_kernelBrowser, "Chromium", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        var uri = new Uri(proxy.Server);
                        var host = uri.Host;
                        var port = uri.Port;
                        var user = proxy.Username;
                        var pass = proxy.Password;

                        bridge = new Socks5Bridge(host, port, user, pass);
                        bridge.Start();

                        bridgeAdapter = new Socks5BridgeAsyncWrapper(bridge);

                        ctxOptions.Proxy = new Microsoft.Playwright.Proxy
                        {
                            Server = $"socks5://127.0.0.1:{bridge.Port}",
                            // leave Username/Password unset when using local bridge
                            Bypass = proxy.Bypass
                        };
                    }
                    catch
                    {
                        // If bridge creation fails, ensure we don't leave a half-started bridge
                        try { bridge?.Dispose(); } catch { }
                        bridge = null;
                        bridgeAdapter = null;
                        // fall back to using the provided proxy directly
                        ctxOptions.Proxy = new Microsoft.Playwright.Proxy
                        {
                            Server = proxy.Server!,
                            Username = proxy.Username,
                            Password = proxy.Password,
                            Bypass = proxy.Bypass
                        };
                    }
                }
                else
                {
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

                var perms = ctxOptions.Permissions?.ToList() ?? new List<string>();
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

                var perms = ctxOptions.Permissions?.ToList() ?? new List<string>();
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
                var _perms = ctxOptions.Permissions?.ToList() ?? new List<string>();
                if (!_perms.Contains("geolocation")) _perms.Add("geolocation");
                ctxOptions.Permissions = _perms;
            }

            // Ensure geolocation permission if set
            if (ctxOptions.Geolocation is not null)
            {
                 var _perms2 = ctxOptions.Permissions?.ToList() ?? new List<string>();
                 if (!_perms2.Contains("geolocation")) _perms2.Add("geolocation");
                 ctxOptions.Permissions = _perms2;
             }

            if (options?.Permissions is not null && options.Permissions.Count > 0)
            {
                var _perms3 = ctxOptions.Permissions?.ToList() ?? new List<string>();
                foreach (var p in options.Permissions)
                {
                    if (!_perms3.Contains(p)) _perms3.Add(p);
                }
                ctxOptions.Permissions = _perms3;
            }

            var context = await _browser.NewContextAsync(ctxOptions);

            // Inject Stealth Scripts
            if (_enableStealth && profile is not null)
            {
                var script = StealthScripts.GetInitScript(profile);
                await context.AddInitScriptAsync(script);
            }

            var sessionId = Guid.NewGuid().ToString();
            return new BrowserSessionWrapper(context, sessionId, () => _sessionLock.Release(), bridgeAdapter);
        }
        catch
        {
            _sessionLock.Release();
            // Ensure bridge is cleaned up if created
            try
            {
                // No await here because bridgeAdapter may be null and DisposeAsync is quick
                // but we can attempt synchronous dispose if wrapper not present
            }
            catch { }
            throw;
        }
    }

    private sealed class Socks5BridgeAsyncWrapper : IAsyncDisposable
    {
        private readonly Socks5Bridge _bridge;
        public Socks5BridgeAsyncWrapper(Socks5Bridge bridge)
        {
            _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                _bridge.Dispose();
            }
            catch { }
            return ValueTask.CompletedTask;
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        try
        {
            await _browser.CloseAsync();
        }
        catch { }

        try
        {
            await _browser.DisposeAsync();
        }
        catch { }

        try
        {
            _playwright.Dispose();
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
        await DisposeAsyncCore();
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            // Block synchronously to run async cleanup
            DisposeAsyncCore().AsTask().GetAwaiter().GetResult();
        }
        _disposed = true;
    }
}
