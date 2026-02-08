using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghost.ProxyManagement;

/// <summary>
/// Maintains a pool of healthy proxies with round-robin rotation and geographic filtering.
/// Auto-removes unhealthy proxies and provides location-based proxy selection.
/// </summary>
public sealed class RotatingProxyPool : IDisposable
{
    private readonly IProxySource _scraper;
    private readonly FreeProxyHealthChecker _healthChecker;
    private readonly ILogger<RotatingProxyPool> _logger;

    private readonly ConcurrentDictionary<string, ProxyPoolEntry> _proxyPool = new();
    private long _roundRobinIndex;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private volatile bool _initialized;

    private static readonly Action<ILogger, int, Exception?> s_logPoolRefreshed =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "PoolRefreshed"),
            "Proxy pool refreshed with {Count} healthy proxies");

    private static readonly Action<ILogger, string, Exception?> s_logProxyRemoved =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, "ProxyRemoved"),
            "Removed unhealthy proxy {Server} from pool");

    public RotatingProxyPool(
        IProxySource scraper,
        FreeProxyHealthChecker healthChecker,
        ILogger<RotatingProxyPool> logger)
    {
        _scraper = scraper ?? throw new ArgumentNullException(nameof(scraper));
        _healthChecker = healthChecker ?? throw new ArgumentNullException(nameof(healthChecker));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the number of healthy proxies currently in the pool.
    /// </summary>
    public int HealthyProxyCount => _proxyPool.Count(p => p.Value.IsHealthy);

    /// <summary>
    /// Gets the total number of proxies in the pool.
    /// </summary>
    public int TotalProxyCount => _proxyPool.Count;

    /// <summary>
    /// Gets the next proxy using round-robin rotation.
    /// </summary>
    public async Task<ProxyInfo?> GetNextProxyAsync(CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        var healthyProxies = GetHealthyProxies();
        if (healthyProxies.Count == 0)
            return null;

        var index = (int)(Interlocked.Increment(ref _roundRobinIndex) % healthyProxies.Count);
        var entry = healthyProxies[index];
        entry.LastUsed = DateTimeOffset.UtcNow;
        entry.UsageCount++;

        return entry.Proxy;
    }

    /// <summary>
    /// Gets a proxy by location (country and optionally city).
    /// </summary>
    public async Task<ProxyInfo?> GetProxyByLocationAsync(string? country = null, string? city = null, CancellationToken ct = default)
    {
        await EnsureInitializedAsync(ct).ConfigureAwait(false);

        var healthyProxies = GetHealthyProxies();
        if (healthyProxies.Count == 0)
            return null;

        // Filter by country if specified
        if (!string.IsNullOrEmpty(country))
        {
            healthyProxies = healthyProxies
                .Where(p => string.Equals(p.Country, country, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        // Filter by city if specified
        if (!string.IsNullOrEmpty(city))
        {
            healthyProxies = healthyProxies
                .Where(p => string.Equals(p.City, city, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (healthyProxies.Count == 0)
            return null;

        // Return the least used proxy from filtered results
        var entry = healthyProxies.OrderBy(p => p.UsageCount).First();
        entry.LastUsed = DateTimeOffset.UtcNow;
        entry.UsageCount++;

        return entry.Proxy;
    }

    /// <summary>
    /// Reports the result of using a proxy to update its health metrics.
    /// </summary>
    public async Task ReportProxyResultAsync(ProxyInfo proxy, bool success, TimeSpan responseTime, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        var key = GetProxyKey(proxy);
        if (!_proxyPool.TryGetValue(key, out var entry))
            return;

        entry.TotalRequests++;
        if (success)
        {
            entry.SuccessfulRequests++;
            entry.LastSuccessTime = DateTimeOffset.UtcNow;
            entry.LastResponseTime = responseTime;
        }
        else
        {
            entry.FailedRequests++;
            entry.ConsecutiveFailures++;
        }

        // Calculate success rate and remove if below threshold
        if (_healthChecker.ShouldRemoveProxy(entry.TotalRequests, entry.SuccessfulRequests))
        {
            if (_proxyPool.TryRemove(key, out _))
            {
                s_logProxyRemoved(_logger, proxy.Server, null);
            }
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Manually refreshes the proxy pool by scraping and health checking new proxies.
    /// </summary>
    public async Task RefreshPoolAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await RefreshPoolCoreAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Core refresh logic that assumes the caller has already acquired _refreshLock.
    /// </summary>
    private async Task RefreshPoolCoreAsync(CancellationToken ct)
    {
        // Scrape proxies
        var proxies = await _scraper.FetchProxiesAsync(ct).ConfigureAwait(false);

        // Health check each proxy
        var healthCheckTasks = proxies.Select(p => _healthChecker.CheckHealthAsync(p, ct));
        var results = await Task.WhenAll(healthCheckTasks).ConfigureAwait(false);

        // Add healthy proxies to pool
        foreach (var result in results.Where(r => r.IsHealthy))
        {
            var key = GetProxyKey(result.Proxy);
            _proxyPool.TryAdd(key, new ProxyPoolEntry
            {
                Proxy = result.Proxy,
                IsHealthy = true,
                LastHealthCheck = result.CheckedAt,
                LastResponseTime = result.ResponseTime,
                AddedAt = DateTimeOffset.UtcNow
            });
        }

        s_logPoolRefreshed(_logger, HealthyProxyCount, null);
    }

    /// <summary>
    /// Gets all proxies currently in the pool.
    /// </summary>
    public IReadOnlyList<ProxyPoolEntry> GetAllProxies()
    {
        return _proxyPool.Values.ToList();
    }

    private async Task EnsureInitializedAsync(CancellationToken ct)
    {
        if (_initialized)
            return;

        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            await RefreshPoolCoreAsync(ct).ConfigureAwait(false);
            _initialized = true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private List<ProxyPoolEntry> GetHealthyProxies()
    {
        return _proxyPool.Values
            .Where(p => p.IsHealthy)
            .OrderBy(p => p.UsageCount)
            .ToList();
    }

    private static string GetProxyKey(ProxyInfo proxy)
    {
        return $"{proxy.Server}|{proxy.Username ?? string.Empty}";
    }

    public void Dispose()
    {
        _refreshLock?.Dispose();
    }
}

/// <summary>
/// Represents a proxy entry in the rotating pool with health and usage metrics.
/// </summary>
public sealed class ProxyPoolEntry
{
    public ProxyInfo Proxy { get; set; } = null!;
    public bool IsHealthy { get; set; }
    public string? Country { get; set; }
    public string? City { get; set; }
    public DateTimeOffset AddedAt { get; set; }
    public DateTimeOffset LastHealthCheck { get; set; }
    public DateTimeOffset? LastUsed { get; set; }
    public DateTimeOffset? LastSuccessTime { get; set; }
    public TimeSpan LastResponseTime { get; set; }
    public long UsageCount { get; set; }
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public int ConsecutiveFailures { get; set; }

    public double SuccessRate => TotalRequests > 0
        ? (double)SuccessfulRequests / TotalRequests
        : 0.0;
}
