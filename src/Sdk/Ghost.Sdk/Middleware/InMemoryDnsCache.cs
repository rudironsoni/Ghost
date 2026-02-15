using System.Collections.Concurrent;
using System.Net;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// In-memory implementation of <see cref="IDnsCache"/> with TTL support and automatic cleanup.
/// </summary>
public sealed class InMemoryDnsCache : IDnsCache, IDisposable
{
    private readonly ConcurrentDictionary<string, DnsCacheEntry> _cache = new();
    private readonly DnsCacheOptions _options;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryDnsCache"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the DNS cache.</param>
    public InMemoryDnsCache(DnsCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, _options.Ttl, _options.Ttl);
    }

    /// <summary>
    /// Resolves a hostname to IP addresses, using cache when available.
    /// </summary>
    /// <param name="hostname">The hostname to resolve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An array of IP addresses for the hostname.</returns>
    public async Task<IPAddress[]> ResolveAsync(string hostname, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(hostname);

        // Try to get from cache first
        if (_cache.TryGetValue(hostname, out DnsCacheEntry? entry) && !entry.IsExpired)
        {
            return entry.Addresses;
        }

        // Cache miss or expired - resolve DNS
        IPAddress[] addresses = await Dns.GetHostAddressesAsync(hostname, ct).ConfigureAwait(false);

        // Store in cache
        DateTimeOffset expiresAt = DateTimeOffset.UtcNow.Add(_options.Ttl);
        _cache[hostname] = new DnsCacheEntry(addresses, expiresAt);

        // Enforce max entries limit
        EnforceMaxEntries();

        return addresses;
    }

    /// <summary>
    /// Invalidates the cached DNS entry for the specified hostname.
    /// </summary>
    /// <param name="hostname">The hostname to invalidate.</param>
    public void Invalidate(string hostname)
    {
        ArgumentNullException.ThrowIfNull(hostname);
        _cache.TryRemove(hostname, out _);
    }

    /// <summary>
    /// Clears all cached DNS entries.
    /// </summary>
    public void Clear()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Removes expired entries from the cache.
    /// </summary>
    private void CleanupExpiredEntries(object? state)
    {
        var expiredKeys = _cache
            .Where(kvp => kvp.Value.IsExpired)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string? key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Enforces the maximum number of cache entries by removing oldest entries.
    /// </summary>
    private void EnforceMaxEntries()
    {
        int cacheCount = _cache.Count;
        if (cacheCount <= _options.MaxEntries)
        {
            return;
        }

        // Remove entries that will expire soonest
        var entriesToRemove = _cache
            .OrderBy(kvp => kvp.Value.ExpiresAt)
            .Take(cacheCount - _options.MaxEntries)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (string? key in entriesToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Disposes resources used by the DNS cache.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer.Dispose();
        _cache.Clear();
        _disposed = true;
    }
}
