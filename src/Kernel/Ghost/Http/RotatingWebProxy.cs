using System;
using System.Net;
using System.Threading;

namespace Ghost.Http;

/// <summary>
/// A rotating proxy implementation that caches proxies to avoid blocking on async operations.
/// Implements IWebProxy interface while maintaining async initialization internally.
/// </summary>
public sealed class RotatingWebProxy : IWebProxy, IDisposable
{
    private readonly IProxyProvider _provider;
    private readonly TimeSpan _cacheDuration;
    private ProxyInfo? _cachedProxy;
    private DateTime _cacheTimestamp = DateTime.MinValue;
    private readonly object _lock = new();
    private readonly Timer? _refreshTimer;
    private readonly TimeProvider _timeProvider;

    public RotatingWebProxy(IProxyProvider provider, TimeSpan? cacheDuration = null, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _provider = provider;
        _cacheDuration = cacheDuration ?? TimeSpan.FromMinutes(5);
        _timeProvider = timeProvider ?? TimeProvider.System;

        // Start background refresh timer - fire-and-forget is intentional
        _refreshTimer = new Timer(_ => Task.Run(RefreshProxyAsync), null, TimeSpan.Zero, _cacheDuration);
    }

    public ICredentials? Credentials { get; set; }

    public Uri GetProxy(Uri destination)
    {
        ProxyInfo? proxy = GetCachedProxy();

        if (proxy is null)
            return destination; // direct connection

        // Build the proxy Uri. If credentials are provided separately on the proxy
        // object, attach them to the URI using UriBuilder so that schemes like
        // socks5://username:password@host:port are produced. URL-encode the
        // username/password to ensure special characters are handled safely.
        var serverUri = new Uri(proxy.Server);
        var uriBuilder = new UriBuilder(serverUri);

        if (!string.IsNullOrEmpty(proxy.Username))
        {
            // UriBuilder will include the UserName/Password parts when creating the Uri.
            // Escape username/password to be safe with special characters.
            uriBuilder.UserName = Uri.EscapeDataString(proxy.Username);
            uriBuilder.Password = Uri.EscapeDataString(proxy.Password ?? string.Empty);
        }

        return uriBuilder.Uri;
    }

    public bool IsBypassed(Uri host)
    {
        // Never bypass; always let GetProxy decide per-request
        return false;
    }

    private ProxyInfo? GetCachedProxy()
    {
        lock (_lock)
        {
            // Return cached proxy if still valid
            if (_cachedProxy is not null && _timeProvider.GetUtcNow().UtcDateTime - _cacheTimestamp < _cacheDuration)
            {
                return _cachedProxy;
            }
        }

        // Trigger async refresh without blocking (fire-and-forget is acceptable here
        // because we either return stale data or direct connection temporarily)
        _ = RefreshProxyAsync();

        lock (_lock)
        {
            // Return cached proxy even if stale, or null if never loaded
            return _cachedProxy;
        }
    }

    private async Task RefreshProxyAsync()
    {
        try
        {
            ProxyInfo? proxy = await _provider.GetProxyAsync("US").ConfigureAwait(false);

            lock (_lock)
            {
                _cachedProxy = proxy;
                _cacheTimestamp = _timeProvider.GetUtcNow().UtcDateTime;
            }
        }
        catch
        {
            // Log error but don't throw - we'll retry on next access
            // Consumer will get direct connection or stale proxy temporarily
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}
