using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Kernel;
using Ghost.ProxyConfiguration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.ProxyIntegration;

/// <summary>
/// Adapts existing proxy sources (StaticProxySource, ApiProxySource) to work
/// with the abstract proxy system. Provides a compatibility layer that bridges
/// legacy proxy source implementations with the modern health-aware system.
/// </summary>
public sealed class ProxySourceAdapter : IProxySource
{
    private readonly Ghost.Kernel.ProxySourceConfig _config;
    private readonly ILogger<ProxySourceAdapter> _logger;
    private readonly HttpClient? _httpClient;
    private readonly Lazy<IProxySource> _adaptedSource;

    private static readonly Action<ILogger, string, Exception?> s_logAdapterInitialized =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(ProxySourceAdapter)),
            "Proxy source adapter initialized for type '{SourceType}'");

    private static readonly Action<ILogger, string, Exception?> s_logAdapterFetching =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, nameof(ProxySourceAdapter)),
            "Fetching proxies from adapted source '{SourceType}'");

    private static readonly Action<ILogger, string, int, Exception?> s_logAdapterFetchSuccess =
        LoggerMessage.Define<string, int>(LogLevel.Information, new EventId(3, nameof(ProxySourceAdapter)),
            "Adapter fetched {Count} proxies from source '{SourceType}'");

    private static readonly Action<ILogger, string, Exception?> s_logAdapterFetchFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(4, nameof(ProxySourceAdapter)),
            "Adapter failed to fetch proxies from source '{SourceType}'");

    private static readonly Action<ILogger, string, Exception?> s_logUnsupportedSourceType =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, nameof(ProxySourceAdapter)),
            "Unsupported proxy source type '{SourceType}', returning empty collection");

    public ProxySourceAdapter(Ghost.Kernel.ProxySourceConfig config, ILogger<ProxySourceAdapter> logger, HttpClient? httpClient = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient;

        _adaptedSource = new Lazy<IProxySource>(() =>
        {
            string sourceType = _config.Type ?? "Static";
            s_logAdapterInitialized(_logger, sourceType, null);

            return sourceType.ToLowerInvariant() switch
            {
                "static" => CreateStaticProxySource(logger),
                "api" => CreateApiProxySource(logger),
                _ => new NullProxySource()
            };
        });
    }

    private Ghost.Services.StaticProxySource CreateStaticProxySource(ILogger<ProxySourceAdapter> logger)
    {
        var coreConfig = new Ghost.Kernel.ProxySourceConfig
        {
            Enabled = _config.Enabled,
            Type = _config.Type,
            Username = _config.Username,
            Password = _config.Password,
            Hosts = _config.Hosts,
            Url = _config.Url
        };

        return new Ghost.Services.StaticProxySource(coreConfig, CreateLoggerAdapter<Ghost.Services.StaticProxySource>(logger));
    }

    private Ghost.Services.ApiProxySource CreateApiProxySource(ILogger<ProxySourceAdapter> logger)
    {
        var coreConfig = new Ghost.Kernel.ProxySourceConfig
        {
            Enabled = _config.Enabled,
            Type = _config.Type,
            Username = _config.Username,
            Password = _config.Password,
            Hosts = _config.Hosts,
            Url = _config.Url
        };

        return new Ghost.Services.ApiProxySource(
            _httpClient ?? new HttpClient(),
            coreConfig,
            CreateLoggerAdapter<Ghost.Services.ApiProxySource>(logger));
    }

    private static LoggerAdapter<T> CreateLoggerAdapter<T>(ILogger<ProxySourceAdapter> logger) where T : class
    {
        return new LoggerAdapter<T>(logger);
    }

    /// <summary>
    /// Fetches proxies from the adapted source, handling errors gracefully.
    /// </summary>
    public async Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
    {
        if (_config == null || !_config.Enabled)
            return Enumerable.Empty<ProxyInfo>();

        try
        {
            string sourceType = _config.Type ?? "Static";
            s_logAdapterFetching(_logger, sourceType, null);

            IEnumerable<ProxyInfo> proxies = await _adaptedSource.Value.FetchProxiesAsync(ct).ConfigureAwait(false);
            var proxyList = proxies.ToList();

            s_logAdapterFetchSuccess(_logger, sourceType, proxyList.Count, null);
            return proxyList;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            s_logAdapterFetchFailed(_logger, _config.Type ?? "Unknown", ex);
            return Enumerable.Empty<ProxyInfo>();
        }
    }

    /// <summary>
    /// Null proxy source that returns empty results for unsupported types.
    /// </summary>
    private sealed class NullProxySource : IProxySource
    {
        public Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
        {
            return Task.FromResult(Enumerable.Empty<ProxyInfo>());
        }
    }
}

/// <summary>
/// Monitors the health of proxy sources by tracking fetch success rates,
/// latency metrics, and error patterns. Enables intelligent fallback decisions.
/// </summary>
public sealed class ProxySourceHealthMonitor
{
    private readonly ConcurrentDictionary<string, ProxySourceHealthMetrics> _sourceMetrics = new();
    private readonly ILogger<ProxySourceHealthMonitor> _logger;
    private readonly ProxySystemOptions _options;

    private static readonly Action<ILogger, string, double, Exception?> s_logSourceHealthy =
        LoggerMessage.Define<string, double>(LogLevel.Debug, new EventId(1, nameof(ProxySourceHealthMonitor)),
            "Proxy source '{Source}' health check passed - Success rate: {SuccessRate:F2}%");

    private static readonly Action<ILogger, string, int, Exception?> s_logSourceDegraded =
        LoggerMessage.Define<string, int>(LogLevel.Warning, new EventId(2, nameof(ProxySourceHealthMonitor)),
            "Proxy source '{Source}' degraded - Consecutive failures: {Failures}");

    private static readonly Action<ILogger, string, Exception?> s_logSourceUnhealthy =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3, nameof(ProxySourceHealthMonitor)),
            "Proxy source '{Source}' marked unhealthy - Consider fallback");

    private static readonly Action<ILogger, string, int, double, Exception?> s_logSourceMetrics =
        LoggerMessage.Define<string, int, double>(LogLevel.Information, new EventId(4, nameof(ProxySourceHealthMonitor)),
            "Source '{Source}' metrics - Total attempts: {Attempts}, Avg latency: {AvgLatency:F2}ms");

    private static readonly Action<ILogger, string, Exception?> s_logSourceRecovered =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(5, nameof(ProxySourceHealthMonitor)),
            "Proxy source '{Source}' has recovered");

    public ProxySourceHealthMonitor(IOptions<ProxySystemOptions> options, ILogger<ProxySourceHealthMonitor> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Reports a proxy source fetch result to update health metrics.
    /// </summary>
    public void ReportSourceResult(string sourceName, bool success, TimeSpan latency, int proxiesFetched = 0)
    {
        if (string.IsNullOrEmpty(sourceName))
            return;

        ProxySourceHealthMetrics metrics = _sourceMetrics.GetOrAdd(sourceName, _ => new ProxySourceHealthMetrics
        {
            SourceName = sourceName,
            FirstSeen = DateTimeOffset.UtcNow
        });

        metrics.TotalAttempts++;
        metrics.LastAttempt = DateTimeOffset.UtcNow;
        metrics.LatencyHistory.Add(latency.TotalMilliseconds);

        if (success)
        {
            metrics.SuccessfulAttempts++;
            metrics.ConsecutiveFailures = 0;
            metrics.ProxiesFetched += proxiesFetched;
        }
        else
        {
            metrics.FailedAttempts++;
            metrics.ConsecutiveFailures++;
            metrics.LastFailure = DateTimeOffset.UtcNow;
        }

        // Log appropriate level based on health status
        double successRate = metrics.SuccessRate;
        if (metrics.ConsecutiveFailures >= 3)
        {
            s_logSourceDegraded(_logger, sourceName, metrics.ConsecutiveFailures, null);
        }
        else if (successRate >= 0.95)
        {
            s_logSourceHealthy(_logger, sourceName, successRate * 100, null);
        }

        // Recovery notification
        if (success && metrics.ConsecutiveFailures > 0)
        {
            s_logSourceRecovered(_logger, sourceName, null);
        }
    }

    /// <summary>
    /// Determines if a proxy source is healthy and usable.
    /// </summary>
    public bool IsSourceHealthy(string sourceName)
    {
        if (!_sourceMetrics.TryGetValue(sourceName, out ProxySourceHealthMetrics? metrics))
            return true; // Unknown source is assumed healthy

        // Unhealthy if too many consecutive failures
        if (metrics.ConsecutiveFailures >= 5)
        {
            s_logSourceUnhealthy(_logger, sourceName, null);
            return false;
        }

        // Unhealthy if success rate drops below 30%
        return metrics.SuccessRate >= 0.30;
    }

    /// <summary>
    /// Gets health metrics for a specific proxy source.
    /// </summary>
    public ProxySourceHealthMetrics? GetSourceMetrics(string sourceName)
    {
        return _sourceMetrics.TryGetValue(sourceName, out ProxySourceHealthMetrics? metrics) ? metrics : null;
    }

    /// <summary>
    /// Gets all source metrics.
    /// </summary>
    public IReadOnlyDictionary<string, ProxySourceHealthMetrics> GetAllSourceMetrics()
    {
        return _sourceMetrics.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Resets health metrics for a source (e.g., after configuration change).
    /// </summary>
    public void ResetSourceMetrics(string sourceName)
    {
        _sourceMetrics.TryRemove(sourceName, out _);
    }
}

/// <summary>
/// Manages fallback between different proxy source types.
/// Enables graceful degradation when primary sources fail.
/// </summary>
public sealed class ProxySourceFallbackManager
{
    private readonly List<Ghost.Kernel.ProxySourceConfig> _sourcesChain;
    private readonly ProxySourceHealthMonitor _healthMonitor;
    private readonly ILogger<ProxySourceFallbackManager> _logger;
    private readonly Dictionary<string, IProxySource> _sourceCache = new();
    private readonly HttpClient? _httpClient;
    private int _currentSourceIndex;

    private static readonly Action<ILogger, string, string, Exception?> s_logFallbackAttempt =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(1, nameof(ProxySourceFallbackManager)),
            "Falling back from '{CurrentSource}' to '{NextSource}'");

    private static readonly Action<ILogger, Exception?> s_logAllSourcesFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(2, nameof(ProxySourceFallbackManager)),
            "All proxy sources in fallback chain have failed");

    private static readonly Action<ILogger, string, Exception?> s_logSourceSelected =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(3, nameof(ProxySourceFallbackManager)),
            "Proxy source '{Source}' selected from fallback chain");

    private static readonly Action<ILogger, int, Exception?> s_logChainInitialized =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(4, nameof(ProxySourceFallbackManager)),
            "Proxy source fallback chain initialized with {Count} sources");

    public ProxySourceFallbackManager(
        List<Ghost.Kernel.ProxySourceConfig> sourcesChain,
        ProxySourceHealthMonitor healthMonitor,
        ILogger<ProxySourceFallbackManager> logger,
        HttpClient? httpClient = null)
    {
        _sourcesChain = sourcesChain ?? throw new ArgumentNullException(nameof(sourcesChain));
        _healthMonitor = healthMonitor ?? throw new ArgumentNullException(nameof(healthMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient;

        s_logChainInitialized(_logger, _sourcesChain.Count, null);
    }

    /// <summary>
    /// Gets the next healthy proxy source from the fallback chain.
    /// </summary>
    public IProxySource? GetNextHealthySource()
    {
        if (_sourcesChain.Count == 0)
            return null;

        int startIndex = _currentSourceIndex;
        int currentIndex = _currentSourceIndex;

        do
        {
            Ghost.Kernel.ProxySourceConfig config = _sourcesChain[currentIndex];
            string sourceName = config.Type ?? $"Source_{currentIndex}";

            if (config.Enabled && _healthMonitor.IsSourceHealthy(sourceName))
            {
                s_logSourceSelected(_logger, sourceName, null);
                _currentSourceIndex = currentIndex;
                return GetOrCreateSource(config);
            }

            currentIndex = (currentIndex + 1) % _sourcesChain.Count;
        } while (currentIndex != startIndex);

        s_logAllSourcesFailed(_logger, null);
        return null;
    }

    /// <summary>
    /// Attempts to fetch proxies from healthy sources with automatic fallback.
    /// </summary>
    public async Task<IEnumerable<ProxyInfo>> FetchProxiesWithFallbackAsync(CancellationToken ct)
    {
        int startIndex = _currentSourceIndex;
        int currentIndex = _currentSourceIndex;

        do
        {
            Ghost.Kernel.ProxySourceConfig config = _sourcesChain[currentIndex];
            if (!config.Enabled)
            {
                currentIndex = (currentIndex + 1) % _sourcesChain.Count;
                continue;
            }

            string sourceName = config.Type ?? $"Source_{currentIndex}";

            try
            {
                IProxySource source = GetOrCreateSource(config);
                var stopwatch = Stopwatch.StartNew();

                IEnumerable<ProxyInfo> proxies = await source.FetchProxiesAsync(ct).ConfigureAwait(false);
                stopwatch.Stop();

                var proxyList = proxies.ToList();
                _healthMonitor.ReportSourceResult(sourceName, proxyList.Count > 0, stopwatch.Elapsed, proxyList.Count);

                if (proxyList.Count > 0)
                {
                    s_logSourceSelected(_logger, sourceName, null);
                    _currentSourceIndex = currentIndex;
                    return proxyList;
                }

                // Try next source if this one returned empty
                currentIndex = (currentIndex + 1) % _sourcesChain.Count;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _healthMonitor.ReportSourceResult(sourceName, false, TimeSpan.Zero);

                int nextIndex = (currentIndex + 1) % _sourcesChain.Count;
                string nextSource = _sourcesChain[nextIndex].Type ?? $"Source_{nextIndex}";

                s_logFallbackAttempt(_logger, sourceName, nextSource, ex);
                currentIndex = nextIndex;
            }
        } while (currentIndex != startIndex);

        s_logAllSourcesFailed(_logger, null);
        return Enumerable.Empty<ProxyInfo>();
    }

    /// <summary>
    /// Reports the result of using a proxy from a specific source.
    /// </summary>
    public void ReportSourceUsageResult(int sourceIndex, bool success, TimeSpan latency)
    {
        if (sourceIndex < 0 || sourceIndex >= _sourcesChain.Count)
            return;

        Ghost.Kernel.ProxySourceConfig config = _sourcesChain[sourceIndex];
        string sourceName = config.Type ?? $"Source_{sourceIndex}";
        _healthMonitor.ReportSourceResult(sourceName, success, latency);
    }

    /// <summary>
    /// Marks a source as failed and triggers fallback.
    /// </summary>
    public void MarkSourceFailed(int sourceIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= _sourcesChain.Count)
            return;

        Ghost.Kernel.ProxySourceConfig config = _sourcesChain[sourceIndex];
        string sourceName = config.Type ?? $"Source_{sourceIndex}";
        _healthMonitor.ReportSourceResult(sourceName, false, TimeSpan.Zero);
    }

    /// <summary>
    /// Resets the fallback chain to start from the primary source.
    /// </summary>
    public void ResetFallbackChain()
    {
        _currentSourceIndex = 0;
    }

    private IProxySource GetOrCreateSource(Ghost.Kernel.ProxySourceConfig config)
    {
        string sourceName = config.Type ?? "Unknown";

        if (_sourceCache.TryGetValue(sourceName, out IProxySource? cached))
            return cached;

        IProxySource? source = sourceName.ToLowerInvariant() switch
        {
            "static" => new Ghost.Services.StaticProxySource(config,
                new SimpleLoggerAdapter<Ghost.Services.StaticProxySource>()) as IProxySource,
            "api" => new Ghost.Services.ApiProxySource(_httpClient ?? new HttpClient(), config,
                new SimpleLoggerAdapter<Ghost.Services.ApiProxySource>()) as IProxySource,
            _ => null
        };

        if (source != null)
        {
            _sourceCache[sourceName] = source;
        }

        return source ?? new NullProxySource();
    }

    private sealed class NullProxySource : IProxySource
    {
        public Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct)
        {
            return Task.FromResult(Enumerable.Empty<ProxyInfo>());
        }
    }

    private sealed class SimpleLoggerAdapter<T> : ILogger<T>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
    }
}

/// <summary>
/// Generic logger adapter to bridge different logger types without performance overhead.
/// </summary>
internal sealed class LoggerAdapter<T> : ILogger<T> where T : class
{
    private readonly ILogger _baseLogger;

    public LoggerAdapter(ILogger baseLogger)
    {
        _baseLogger = baseLogger ?? throw new ArgumentNullException(nameof(baseLogger));
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        => _baseLogger.BeginScope(state);

    public bool IsEnabled(LogLevel logLevel)
        => _baseLogger.IsEnabled(logLevel);

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        => _baseLogger.Log(logLevel, eventId, state, exception, formatter);
}

/// <summary>
/// Health and performance metrics for a proxy source.
/// </summary>
public sealed class ProxySourceHealthMetrics
{
    /// <summary>
    /// Name of the proxy source.
    /// </summary>
    public string SourceName { get; set; } = string.Empty;

    /// <summary>
    /// When this source was first tracked.
    /// </summary>
    public DateTimeOffset FirstSeen { get; set; }

    /// <summary>
    /// Last time a fetch attempt was made.
    /// </summary>
    public DateTimeOffset LastAttempt { get; set; }

    /// <summary>
    /// Last time a fetch failed.
    /// </summary>
    public DateTimeOffset? LastFailure { get; set; }

    /// <summary>
    /// Total fetch attempts from this source.
    /// </summary>
    public int TotalAttempts { get; set; }

    /// <summary>
    /// Successful fetch attempts.
    /// </summary>
    public int SuccessfulAttempts { get; set; }

    /// <summary>
    /// Failed fetch attempts.
    /// </summary>
    public int FailedAttempts { get; set; }

    /// <summary>
    /// Number of consecutive failures.
    /// </summary>
    public int ConsecutiveFailures { get; set; }

    /// <summary>
    /// Total proxies fetched from this source.
    /// </summary>
    public int ProxiesFetched { get; set; }

    /// <summary>
    /// Latency measurements (in milliseconds).
    /// </summary>
    public List<double> LatencyHistory { get; } = new();

    /// <summary>
    /// Calculates the success rate (0.0 to 1.0).
    /// </summary>
    public double SuccessRate => TotalAttempts > 0
        ? (double)SuccessfulAttempts / TotalAttempts
        : 0.0;

    /// <summary>
    /// Calculates average latency in milliseconds.
    /// </summary>
    public double AverageLatency => LatencyHistory.Count > 0
        ? LatencyHistory.Average()
        : 0.0;

    /// <summary>
    /// Calculates median latency in milliseconds.
    /// </summary>
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

    /// <summary>
    /// Calculates 95th percentile latency.
    /// </summary>
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
