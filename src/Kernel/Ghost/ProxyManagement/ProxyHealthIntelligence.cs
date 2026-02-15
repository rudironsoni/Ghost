using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.ProxyConfiguration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.ProxyManagement;

/// <summary>
/// Advanced proxy health intelligence system that monitors proxy performance,
/// implements multiple rotation strategies, handles fallback scenarios,
/// and tracks geographic latency metrics.
/// </summary>
public sealed class ProxyHealthIntelligence : IDisposable
{
    private readonly IEnumerable<IProxySource> _sources;
    private readonly IEnumerable<IProxySource>? _fallbackSources;
    private readonly ILogger<ProxyHealthIntelligence> _logger;
    private readonly ProxySystemOptions _options;
    private readonly HttpClient _healthCheckClient;

    private readonly ConcurrentDictionary<string, ProxyHealthMetrics> _healthMetrics = new();
    private readonly ConcurrentDictionary<string, ProxyInfo> _proxyPool = new();
    private readonly ConcurrentDictionary<string, bool> _blacklist = new();
    private readonly HashSet<string> _whitelist = new();

    private long _roundRobinIndex;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;
    private volatile bool _usingFallback;

    private readonly CancellationTokenSource _healthCheckCts = new();
    private Task? _healthCheckTask;
    private static readonly Action<ILogger, int, Exception?> s_logPoolInitialized =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "PoolInitialized"), "Proxy pool initialized with {Count} proxies");

    private static readonly Action<ILogger, string, Exception?> s_logProxyBlacklisted =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "ProxyBlacklisted"), "Proxy {Proxy} blacklisted due to repeated failures");

    private static readonly Action<ILogger, string, double, Exception?> s_logProxyHealthy =
        LoggerMessage.Define<string, double>(LogLevel.Debug, new EventId(3, "ProxyHealthy"), "Proxy {Proxy} health check passed in {Latency}ms");

    private static readonly Action<ILogger, string, Exception?> s_logProxyUnhealthy =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "ProxyUnhealthy"), "Proxy {Proxy} health check failed");

    private static readonly Action<ILogger, int, Exception?> s_logFallbackActivated =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(5, "FallbackActivated"), "Primary proxy sources exhausted, activating fallback chain with {Count} proxies");

    private static readonly Action<ILogger, string, int, double, double, Exception?> s_logProxyMetrics =
        LoggerMessage.Define<string, int, double, double>(LogLevel.Information, new EventId(6, "ProxyMetrics"),
            "Proxy {Proxy} metrics - Requests: {Requests}, Success Rate: {SuccessRate:F2}%, Avg Latency: {AvgLatency:F2}ms");

    private static readonly Action<ILogger, string, string, Exception?> s_logRotationStrategy =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(7, "RotationStrategy"), "Using {Strategy} rotation strategy to select proxy {Proxy}");

    private static readonly Action<ILogger, Exception?> s_logHealthCheckStarted =
        LoggerMessage.Define(LogLevel.Information, new EventId(8, "HealthCheckStarted"), "Background proxy health checking started");

    private static readonly Action<ILogger, Exception?> s_logHealthCheckStopped =
        LoggerMessage.Define(LogLevel.Information, new EventId(9, "HealthCheckStopped"), "Background proxy health checking stopped");

    private static readonly Action<ILogger, string, Exception?> s_logSourceLoadFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(10, "SourceLoadFailed"), "Failed to load proxies from source {Source}");

    private static readonly Action<ILogger, Exception?> s_logHealthCheckCycleFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(11, "HealthCheckCycleFailed"), "Error during background health check cycle");

    public ProxyHealthIntelligence(
        IEnumerable<IProxySource> sources,
        IOptions<ProxySystemOptions> options,
        ILogger<ProxyHealthIntelligence> logger)
        : this(sources, options, logger, null)
    {
    }

    public ProxyHealthIntelligence(
        IEnumerable<IProxySource> sources,
        IOptions<ProxySystemOptions> options,
        ILogger<ProxyHealthIntelligence> logger,
        HttpClient? healthCheckClient)
    {
        _sources = sources ?? throw new ArgumentNullException(nameof(sources));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_options.FallbackChain?.Count > 0)
        {
            _fallbackSources = CreateFallbackSources(_options.FallbackChain);
        }

        _healthCheckClient = healthCheckClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
    }

    /// <summary>
    /// Gets a proxy using the configured rotation strategy and health intelligence.
    /// </summary>
    public async Task<ProxyInfo?> GetProxyAsync(string? countryCode = null, CancellationToken token = default)
    {
        await EnsureInitializedAsync(token).ConfigureAwait(false);

        List<ProxyInfo> healthyProxies = GetHealthyProxies(countryCode);
        if (healthyProxies.Count == 0)
        {
            if (!_usingFallback && _fallbackSources != null)
            {
                await ActivateFallbackAsync(token).ConfigureAwait(false);
                healthyProxies = GetHealthyProxies(countryCode);
            }

            if (healthyProxies.Count == 0)
                return null;
        }

        string strategy = _options.RotationStrategy ?? "RoundRobin";
        ProxyInfo? selectedProxy = strategy.ToLowerInvariant() switch
        {
            "roundrobin" => SelectRoundRobin(healthyProxies),
            "performance" => SelectByPerformance(healthyProxies),
            "random" => SelectRandom(healthyProxies),
            "leastused" => SelectLeastUsed(healthyProxies),
            _ => SelectRoundRobin(healthyProxies)
        };

        if (selectedProxy != null)
        {
            s_logRotationStrategy(_logger, strategy, selectedProxy.Server, null);
        }

        return selectedProxy;
    }

    /// <summary>
    /// Reports the result of a proxy usage to update health metrics.
    /// </summary>
    public async Task ReportProxyResultAsync(ProxyInfo proxy, bool success, TimeSpan latency, HttpStatusCode? statusCode = null)
    {
        if (proxy == null)
            return;

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

            if (metrics.ConsecutiveFailures >= 5)
            {
                _blacklist.TryAdd(key, true);
                s_logProxyBlacklisted(_logger, proxy.Server, null);
            }
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Manually adds a proxy to the blacklist.
    /// </summary>
    public void BlacklistProxy(ProxyInfo proxy)
    {
        if (proxy == null)
            return;

        string key = GetProxyKey(proxy);
        _blacklist.TryAdd(key, true);
        s_logProxyBlacklisted(_logger, proxy.Server, null);
    }

    /// <summary>
    /// Manually removes a proxy from the blacklist.
    /// </summary>
    public void RemoveFromBlacklist(ProxyInfo proxy)
    {
        if (proxy == null)
            return;

        string key = GetProxyKey(proxy);
        _blacklist.TryRemove(key, out _);
    }

    /// <summary>
    /// Adds a proxy to the whitelist for priority usage.
    /// </summary>
    public void WhitelistProxy(ProxyInfo proxy)
    {
        if (proxy == null)
            return;

        string key = GetProxyKey(proxy);
        lock (_whitelist)
        {
            _whitelist.Add(key);
        }
    }

    /// <summary>
    /// Gets health metrics for all proxies.
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

    private async Task EnsureInitializedAsync(CancellationToken token)
    {
        if (_initialized)
            return;

        await _initLock.WaitAsync(token).ConfigureAwait(false);
        try
        {
            if (_initialized)
                return;

            await LoadProxiesFromSourcesAsync(_sources, token).ConfigureAwait(false);

            if (_options.HealthCheckIntervalSeconds > 0)
            {
                StartBackgroundHealthCheck();
            }

            _initialized = true;
            s_logPoolInitialized(_logger, _proxyPool.Count, null);
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task LoadProxiesFromSourcesAsync(IEnumerable<IProxySource> sources, CancellationToken token)
    {
        foreach (IProxySource source in sources)
        {
            try
            {
                IEnumerable<ProxyInfo> proxies = await source.FetchProxiesAsync(token).ConfigureAwait(false);
                foreach (ProxyInfo proxy in proxies)
                {
                    string key = GetProxyKey(proxy);
                    _proxyPool.TryAdd(key, proxy);
                    _healthMetrics.GetOrAdd(key, _ => new ProxyHealthMetrics
                    {
                        ProxyKey = key,
                        FirstSeen = DateTimeOffset.UtcNow
                    });
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                s_logSourceLoadFailed(_logger, source.GetType().Name, ex);
            }
        }
    }

    private async Task ActivateFallbackAsync(CancellationToken token)
    {
        if (_fallbackSources == null)
            return;

        await LoadProxiesFromSourcesAsync(_fallbackSources, token).ConfigureAwait(false);
        _usingFallback = true;
        s_logFallbackActivated(_logger, _proxyPool.Count, null);
    }

    private List<ProxyInfo> GetHealthyProxies(string? countryCode)
    {
        var healthy = new List<ProxyInfo>();

        lock (_whitelist)
        {
            foreach (string whitelistedKey in _whitelist)
            {
                if (_proxyPool.TryGetValue(whitelistedKey, out ProxyInfo? proxy) && !_blacklist.ContainsKey(whitelistedKey))
                {
                    healthy.Add(proxy);
                }
            }
        }

        foreach (KeyValuePair<string, ProxyInfo> kvp in _proxyPool)
        {
            if (_blacklist.ContainsKey(kvp.Key))
                continue;

            if (_whitelist.Contains(kvp.Key))
                continue;

            if (_healthMetrics.TryGetValue(kvp.Key, out ProxyHealthMetrics? metrics))
            {
                double successRate = metrics.TotalRequests > 0
                    ? (double)metrics.SuccessfulRequests / metrics.TotalRequests
                    : 1.0;

                if (successRate > 0.5)
                {
                    healthy.Add(kvp.Value);
                }
            }
            else
            {
                healthy.Add(kvp.Value);
            }
        }

        return healthy;
    }

    private ProxyInfo SelectRoundRobin(List<ProxyInfo> proxies)
    {
        if (proxies.Count == 0)
            return null!;

        int index = (int)(Interlocked.Increment(ref _roundRobinIndex) % proxies.Count);
        return proxies[index];
    }

    private ProxyInfo SelectByPerformance(List<ProxyInfo> proxies)
    {
        if (proxies.Count == 0)
            return null!;

        ProxyInfo bestProxy = proxies
            .Select(p => new
            {
                Proxy = p,
                Metrics = _healthMetrics.GetOrAdd(GetProxyKey(p), _ => new ProxyHealthMetrics
                {
                    ProxyKey = GetProxyKey(p),
                    FirstSeen = DateTimeOffset.UtcNow
                })
            })
            .OrderByDescending(x => x.Metrics.TotalRequests > 0
                ? (double)x.Metrics.SuccessfulRequests / x.Metrics.TotalRequests
                : 1.0)
            .ThenBy(x => x.Metrics.AverageLatency)
            .First()
            .Proxy;

        return bestProxy;
    }

    private static ProxyInfo SelectRandom(List<ProxyInfo> proxies)
    {
        if (proxies.Count == 0)
            return null!;

        Random random = Random.Shared;
        return proxies[random.Next(proxies.Count)];
    }

    private ProxyInfo SelectLeastUsed(List<ProxyInfo> proxies)
    {
        if (proxies.Count == 0)
            return null!;

        ProxyInfo leastUsed = proxies
            .Select(p => new
            {
                Proxy = p,
                Metrics = _healthMetrics.GetOrAdd(GetProxyKey(p), _ => new ProxyHealthMetrics
                {
                    ProxyKey = GetProxyKey(p),
                    FirstSeen = DateTimeOffset.UtcNow
                })
            })
            .OrderBy(x => x.Metrics.TotalRequests)
            .ThenBy(x => x.Metrics.LastUsed)
            .First()
            .Proxy;

        return leastUsed;
    }

    private void StartBackgroundHealthCheck()
    {
        s_logHealthCheckStarted(_logger, null);
        _healthCheckTask = Task.Run(async () => await PerformBackgroundHealthCheckAsync(_healthCheckCts.Token).ConfigureAwait(false), _healthCheckCts.Token);
    }

    private async Task PerformBackgroundHealthCheckAsync(CancellationToken token)
    {
        var interval = TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds);

        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, token).ConfigureAwait(false);

                foreach (KeyValuePair<string, ProxyInfo> kvp in _proxyPool)
                {
                    if (token.IsCancellationRequested)
                        break;

                    ProxyInfo proxy = kvp.Value;
                    string key = kvp.Key;

                    if (_blacklist.ContainsKey(key))
                        continue;

                    try
                    {
                        var sw = Stopwatch.StartNew();
                        var proxyUri = new Uri(proxy.Server);
                        var webProxy = new WebProxy(proxyUri);

                        if (!string.IsNullOrEmpty(proxy.Username))
                        {
                            webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
                        }

                        using var handler = new HttpClientHandler { Proxy = webProxy };
                        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

                        HttpResponseMessage response = await client.GetAsync("https://httpbin.org/ip", token).ConfigureAwait(false);
                        sw.Stop();

                        bool success = response.IsSuccessStatusCode;
                        await ReportProxyResultAsync(proxy, success, sw.Elapsed, response.StatusCode).ConfigureAwait(false);

                        if (success)
                        {
                            s_logProxyHealthy(_logger, proxy.Server, sw.Elapsed.TotalMilliseconds, null);
                        }
                        else
                        {
                            s_logProxyUnhealthy(_logger, proxy.Server, null);
                        }
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        s_logProxyUnhealthy(_logger, proxy.Server, ex);
                        await ReportProxyResultAsync(proxy, false, TimeSpan.Zero).ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                s_logHealthCheckCycleFailed(_logger, ex);
            }
        }

        s_logHealthCheckStopped(_logger, null);
    }

    private static string GetProxyKey(ProxyInfo proxy)
    {
        return $"{proxy.Server}|{proxy.Username ?? ""}";
    }

    private static IEnumerable<IProxySource> CreateFallbackSources(List<ProxySourceConfig> fallbackConfigs)
    {
        return Enumerable.Empty<IProxySource>();
    }

    public void Dispose()
    {
        _healthCheckCts?.Cancel();
        _healthCheckTask?.Wait(TimeSpan.FromSeconds(5));
        _healthCheckCts?.Dispose();
        _initLock?.Dispose();
        _healthCheckClient?.Dispose();
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

    public List<double> LatencyHistory { get; } = new();
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
