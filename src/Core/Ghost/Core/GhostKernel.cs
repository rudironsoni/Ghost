using Microsoft.Extensions.Logging.Abstractions;
using System.IO;
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
    private bool _disposed;

    private GhostKernel(IPlaywright playwright, IBrowser browser, int maxConcurrentSessions, bool enableStealth)
    {
        ArgumentNullException.ThrowIfNull(playwright);
        ArgumentNullException.ThrowIfNull(browser);
        _playwright = playwright;
        _browser = browser;
        _sessionLock = new SemaphoreSlim(maxConcurrentSessions, maxConcurrentSessions);
        _enableStealth = enableStealth;
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

            return new GhostKernel(playwright, browser, opts.MaxConcurrentSessions, opts.EnableStealth);
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

            if (options?.Proxy is not null)
            {
                ctxOptions.Proxy = new Microsoft.Playwright.Proxy
                {
                    Server = options.Proxy.Server,
                    Username = options.Proxy.Username,
                    Password = options.Proxy.Password,
                    Bypass = options.Proxy.Bypass
                };
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
            return new BrowserSessionWrapper(context, sessionId, () => _sessionLock.Release());
        }
        catch
        {
            _sessionLock.Release();
            throw;
        }
    }

    private async ValueTask DisposeAsyncCore()
    {
        await _browser.DisposeAsync();
        _playwright.Dispose();
        _sessionLock.Dispose();
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
