using System.Collections.Concurrent;
using Ghost.ProxyConfiguration;
using Microsoft.Extensions.Logging;

namespace Ghost.ProxyManagement;

/// <summary>
/// Defines the available proxy rotation strategies.
/// </summary>
public enum RotationStrategyType
{
    RoundRobin,
    Performance,
    Random,
    LeastUsed
}

/// <summary>
/// Implements various proxy rotation strategies.
/// Single responsibility: Selects proxies based on configured strategy.
/// </summary>
public sealed class ProxyRotationStrategy
{
    private readonly ProxyHealthTracker _healthTracker;
    private readonly ILogger? _logger;
    private long _roundRobinIndex;

    private static readonly Action<ILogger, string, string, Exception?> s_logRotationStrategy =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, "RotationStrategy"), "Using {Strategy} rotation strategy to select proxy {Proxy}");

    public ProxyRotationStrategy(ProxyHealthTracker healthTracker, ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(healthTracker);
        _healthTracker = healthTracker;
        _logger = logger;
    }

    /// <summary>
    /// Selects a proxy from the available pool using the specified strategy.
    /// </summary>
    public ProxyInfo? SelectProxy(
        IEnumerable<ProxyInfo> availableProxies,
        RotationStrategyType strategy = RotationStrategyType.RoundRobin)
    {
        List<ProxyInfo> proxies = availableProxies.ToList();
        if (proxies.Count == 0)
            return null;

        ProxyInfo? selected = strategy switch
        {
            RotationStrategyType.RoundRobin => SelectRoundRobin(proxies),
            RotationStrategyType.Performance => SelectByPerformance(proxies),
            RotationStrategyType.Random => SelectRandom(proxies),
            RotationStrategyType.LeastUsed => SelectLeastUsed(proxies),
            _ => SelectRoundRobin(proxies)
        };

        if (selected != null && _logger != null)
        {
            s_logRotationStrategy(_logger, strategy.ToString(), selected.Server, null);
        }

        return selected;
    }

    /// <summary>
    /// Selects a proxy using round-robin rotation.
    /// </summary>
    public ProxyInfo? SelectRoundRobin(List<ProxyInfo> proxies)
    {
        if (proxies.Count == 0)
            return null;

        int index = (int)(Interlocked.Increment(ref _roundRobinIndex) % proxies.Count);
        return proxies[index];
    }

    /// <summary>
    /// Selects the best performing proxy based on success rate and latency.
    /// </summary>
    public ProxyInfo? SelectByPerformance(List<ProxyInfo> proxies)
    {
        if (proxies.Count == 0)
            return null;

        ProxyInfo? bestProxy = proxies
            .Select(p => new
            {
                Proxy = p,
                Metrics = _healthTracker.GetOrCreateMetrics(p)
            })
            .OrderByDescending(x => x.Metrics.TotalRequests > 0
                ? (double)x.Metrics.SuccessfulRequests / x.Metrics.TotalRequests
                : 1.0)
            .ThenBy(x => x.Metrics.AverageLatency)
            .FirstOrDefault()
            ?.Proxy;

        return bestProxy ?? proxies[0];
    }

    /// <summary>
    /// Selects a random proxy from the pool.
    /// </summary>
    public static ProxyInfo? SelectRandom(List<ProxyInfo> proxies)
    {
        if (proxies.Count == 0)
            return null;

        Random random = Random.Shared;
        return proxies[random.Next(proxies.Count)];
    }

    /// <summary>
    /// Selects the least recently used proxy.
    /// </summary>
    public ProxyInfo? SelectLeastUsed(List<ProxyInfo> proxies)
    {
        if (proxies.Count == 0)
            return null;

        ProxyInfo? leastUsed = proxies
            .Select(p => new
            {
                Proxy = p,
                Metrics = _healthTracker.GetOrCreateMetrics(p)
            })
            .OrderBy(x => x.Metrics.TotalRequests)
            .ThenBy(x => x.Metrics.LastUsed)
            .FirstOrDefault()
            ?.Proxy;

        return leastUsed ?? proxies[0];
    }

    /// <summary>
    /// Filters proxies based on health criteria and returns healthy ones.
    /// </summary>
    public List<ProxyInfo> GetHealthyProxies(
        IEnumerable<KeyValuePair<string, ProxyInfo>> proxyPool,
        ProxyBlacklistManager blacklistManager,
        double minSuccessRate = 0.5)
    {
        List<ProxyInfo> healthy = [];

        // First add whitelisted proxies
        foreach (ProxyInfo proxy in blacklistManager.GetWhitelistedProxies(proxyPool))
        {
            if (!blacklistManager.IsBlacklisted(proxy))
            {
                healthy.Add(proxy);
            }
        }

        // Then add other healthy proxies
        foreach (KeyValuePair<string, ProxyInfo> kvp in proxyPool)
        {
            string key = kvp.Key;
            ProxyInfo proxy = kvp.Value;

            if (blacklistManager.IsBlacklisted(proxy))
                continue;

            if (blacklistManager.IsWhitelisted(proxy))
                continue;

            ProxyHealthMetrics? metrics = _healthTracker.GetMetrics(proxy);
            if (metrics != null)
            {
                double successRate = metrics.TotalRequests > 0
                    ? (double)metrics.SuccessfulRequests / metrics.TotalRequests
                    : 1.0;

                if (successRate > minSuccessRate)
                {
                    healthy.Add(proxy);
                }
            }
            else
            {
                // No metrics yet, assume healthy
                healthy.Add(proxy);
            }
        }

        return healthy;
    }

    /// <summary>
    /// Parses a rotation strategy from a string.
    /// </summary>
    public static RotationStrategyType ParseStrategy(string? strategyName)
    {
        if (string.IsNullOrEmpty(strategyName))
            return RotationStrategyType.RoundRobin;

        return strategyName.ToLowerInvariant() switch
        {
            "roundrobin" => RotationStrategyType.RoundRobin,
            "performance" => RotationStrategyType.Performance,
            "random" => RotationStrategyType.Random,
            "leastused" => RotationStrategyType.LeastUsed,
            _ => RotationStrategyType.RoundRobin
        };
    }
}
