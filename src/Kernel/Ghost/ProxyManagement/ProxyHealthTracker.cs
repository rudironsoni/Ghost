using System.Collections.Concurrent;
using System.Net;
using Ghost.ProxyConfiguration;
using Microsoft.Extensions.Logging;

namespace Ghost.ProxyManagement;

/// <summary>
/// Tracks health metrics for proxy servers.
/// Single responsibility: Maintains health statistics for each proxy.
/// </summary>
public sealed class ProxyHealthTracker
{
    private readonly ConcurrentDictionary<string, ProxyHealthMetrics> _healthMetrics = new();
    private readonly ILogger? _logger;

    private static readonly Action<ILogger, string, Exception?> s_logProxyBlacklisted =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1, "ProxyBlacklisted"), "Proxy {Proxy} blacklisted due to repeated failures");

    public ProxyHealthTracker(ILogger? logger = null)
    {
        _logger = logger;
    }

    /// <summary>
    /// Records the result of a proxy request and updates health metrics.
    /// </summary>
    public Task RecordResultAsync(ProxyInfo proxy, bool success, TimeSpan latency, HttpStatusCode? statusCode = null)
    {
        if (proxy == null)
            return Task.CompletedTask;

        string key = GetProxyKey(proxy);
        ProxyHealthMetrics metrics = _healthMetrics.GetOrAdd(key, _ => new ProxyHealthMetrics
        {
            ProxyKey = key,
            FirstSeen = DateTimeOffset.UtcNow
        });

        metrics.TotalRequests++;
        metrics.LastUsed = DateTimeOffset.UtcNow;
        metrics.LatencyHistory.Add(latency.TotalMilliseconds);

        if (success)
        {
            metrics.SuccessfulRequests++;
            metrics.ConsecutiveFailures = 0;
        }
        else
        {
            metrics.FailedRequests++;
            metrics.ConsecutiveFailures++;
            metrics.LastFailure = DateTimeOffset.UtcNow;

            if (metrics.ConsecutiveFailures >= 5 && _logger != null)
            {
                s_logProxyBlacklisted(_logger, proxy.Server, null);
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets health metrics for all tracked proxies.
    /// </summary>
    public IReadOnlyDictionary<string, ProxyHealthMetrics> GetAllMetrics()
    {
        return _healthMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Gets metrics for a specific proxy.
    /// </summary>
    public ProxyHealthMetrics? GetMetrics(ProxyInfo proxy)
    {
        if (proxy == null)
            return null;

        string key = GetProxyKey(proxy);
        return _healthMetrics.TryGetValue(key, out ProxyHealthMetrics? metrics) ? metrics : null;
    }

    /// <summary>
    /// Gets or creates metrics for a proxy.
    /// </summary>
    public ProxyHealthMetrics GetOrCreateMetrics(ProxyInfo proxy)
    {
        string key = GetProxyKey(proxy);
        return _healthMetrics.GetOrAdd(key, _ => new ProxyHealthMetrics
        {
            ProxyKey = key,
            FirstSeen = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Checks if a proxy should be considered unhealthy based on consecutive failures.
    /// </summary>
    public bool IsProxyUnhealthy(ProxyInfo proxy)
    {
        ProxyHealthMetrics? metrics = GetMetrics(proxy);
        return metrics?.ConsecutiveFailures >= 5;
    }

    /// <summary>
    /// Calculates the success rate for a proxy.
    /// </summary>
    public double GetSuccessRate(ProxyInfo proxy)
    {
        ProxyHealthMetrics? metrics = GetMetrics(proxy);
        return metrics?.SuccessRate ?? 1.0;
    }

    /// <summary>
    /// Gets the average latency for a proxy.
    /// </summary>
    public double GetAverageLatency(ProxyInfo proxy)
    {
        ProxyHealthMetrics? metrics = GetMetrics(proxy);
        return metrics?.AverageLatency ?? 0.0;
    }

    /// <summary>
    /// Resets metrics for a specific proxy.
    /// </summary>
    public void ResetMetrics(ProxyInfo proxy)
    {
        if (proxy == null)
            return;

        string key = GetProxyKey(proxy);
        if (_healthMetrics.TryRemove(key, out ProxyHealthMetrics? metrics))
        {
            _healthMetrics.TryAdd(key, new ProxyHealthMetrics
            {
                ProxyKey = key,
                FirstSeen = DateTimeOffset.UtcNow
            });
        }
    }

    /// <summary>
    /// Gets all tracked proxy keys.
    /// </summary>
    public IEnumerable<string> GetTrackedProxyKeys()
    {
        return _healthMetrics.Keys;
    }

    /// <summary>
    /// Checks if a proxy is being tracked.
    /// </summary>
    public bool IsTracked(ProxyInfo proxy)
    {
        string key = GetProxyKey(proxy);
        return _healthMetrics.ContainsKey(key);
    }

    private static string GetProxyKey(ProxyInfo proxy)
    {
        return $"{proxy.Server}|{proxy.Username ?? ""}";
    }
}

/// <summary>
/// Health and performance metrics for a proxy.
/// </summary>
public class ProxyHealthMetrics
{
    public string ProxyKey { get; set; } = string.Empty;
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastUsed { get; set; }
    public DateTimeOffset? LastFailure { get; set; }

    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public int ConsecutiveFailures { get; set; }

    public List<double> LatencyHistory { get; } = [];
    public Dictionary<string, List<double>> GeographicLatency { get; } = new();

    public double SuccessRate => TotalRequests > 0
        ? (double)SuccessfulRequests / TotalRequests
        : 0.0;

    public double AverageLatency => LatencyHistory.Count > 0
        ? LatencyHistory.Average()
        : 0.0;

    public double MedianLatency
    {
        get
        {
            if (LatencyHistory.Count == 0)
                return 0.0;

            var sorted = LatencyHistory.OrderBy(x => x).ToList();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 0
                ? (sorted[mid - 1] + sorted[mid]) / 2.0
                : sorted[mid];
        }
    }

    public double P95Latency
    {
        get
        {
            if (LatencyHistory.Count == 0)
                return 0.0;

            var sorted = LatencyHistory.OrderBy(x => x).ToList();
            int index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
            return sorted[Math.Max(0, index)];
        }
    }
}
