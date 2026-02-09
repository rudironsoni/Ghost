using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// In-memory implementation of <see cref="IHttpCache"/> with TTL support and automatic cleanup.
/// </summary>
public sealed class InMemoryHttpCache : IHttpCache, IDisposable
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly HttpCacheOptions _options;
    private readonly Timer _cleanupTimer;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryHttpCache"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the cache.</param>
    public InMemoryHttpCache(HttpCacheOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
        _cleanupTimer = new Timer(CleanupExpiredEntries, null, _options.CleanupInterval, _options.CleanupInterval);
    }

    /// <summary>
    /// Attempts to retrieve a cached response for the given request.
    /// </summary>
    public Task<bool> TryGetAsync(IRequest request, out IResponse? response, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var key = GetCacheKey(request);
        if (_cache.TryGetValue(key, out var entry) && !entry.IsExpired)
        {
            response = entry.Response;
            return Task.FromResult(true);
        }

        response = null;
        return Task.FromResult(false);
    }

    /// <summary>
    /// Stores a response in the cache for the given request.
    /// </summary>
    public Task SetAsync(IRequest request, IResponse response, TimeSpan? ttl = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(response);

        var key = GetCacheKey(request);
        var effectiveTtl = ttl ?? _options.DefaultTtl;
        var expiresAt = DateTimeOffset.UtcNow + effectiveTtl;

        _cache[key] = new CacheEntry(response, expiresAt);

        // Enforce max cache size by removing oldest entries if needed
        EnforceMaxCacheSize();

        return Task.CompletedTask;
    }

    /// <summary>
    /// Invalidates cache entries matching the specified pattern.
    /// </summary>
    public Task InvalidateAsync(string pattern, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        var regex = new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase);
        var keysToRemove = _cache.Keys.Where(key => regex.IsMatch(key)).ToList();

        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Generates a cache key for the given request based on HTTP method and URL.
    /// </summary>
    private static string GetCacheKey(IRequest request)
    {
        return $"{request.Method}:{request.Url}";
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

        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Enforces the maximum cache size by removing oldest entries.
    /// </summary>
    private void EnforceMaxCacheSize()
    {
        // Approximate cache size (this is a simplified approach)
        // In a production system, you'd want to track actual memory usage
        var cacheCount = _cache.Count;
        var maxEntries = (int)(_options.MaxCacheSize / 10240); // Assume ~10KB per entry

        if (cacheCount > maxEntries)
        {
            // Remove entries that will expire soonest
            var entriesToRemove = _cache
                .OrderBy(kvp => kvp.Value.ExpiresAt)
                .Take(cacheCount - maxEntries)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in entriesToRemove)
            {
                _cache.TryRemove(key, out _);
            }
        }
    }

    /// <summary>
    /// Disposes resources used by the cache.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        _cleanupTimer.Dispose();
        _cache.Clear();
        _disposed = true;
    }
}
