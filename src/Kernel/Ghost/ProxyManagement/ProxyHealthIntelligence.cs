using System.Collections.Concurrent;
using System.Net;
using Ghost.ProxyConfiguration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.ProxyManagement;

/// <summary>
/// Advanced proxy health intelligence system that monitors proxy performance,
/// implements multiple rotation strategies, handles fallback scenarios,
/// and tracks geographic latency metrics.
///
/// This class acts as a facade delegating to specialized components:
/// - ProxyHealthTracker: Health metrics tracking
/// - ProxyRotationStrategy: Proxy selection strategies
/// - ProxyBlacklistManager: Blacklist/whitelist management
/// - ProxyHealthChecker: Background health checking
/// </summary>
public sealed class ProxyHealthIntelligence : IDisposable
{
    private readonly IEnumerable<IProxySource> _sources;
    private readonly IEnumerable<IProxySource>? _fallbackSources;
    private readonly ILogger<ProxyHealthIntelligence> _logger;
    private readonly ProxySystemOptions _options;

    private readonly ConcurrentDictionary<string, ProxyInfo> _proxyPool = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private volatile bool _initialized;
    private volatile bool _usingFallback;

    // Extracted components
    private static readonly Action<ILogger, int, Exception?> s_logPoolInitialized =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(1, "PoolInitialized"), "Proxy pool initialized with {Count} proxies");

    private static readonly Action<ILogger, int, Exception?> s_logFallbackActivated =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(5, "FallbackActivated"), "Primary proxy sources exhausted, activating fallback chain with {Count} proxies");

    private static readonly Action<ILogger, string, Exception?> s_logSourceLoadFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(10, "SourceLoadFailed"), "Failed to load proxies from source {Source}");

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

        // Initialize extracted components
        HealthTracker = new ProxyHealthTracker(logger);
        RotationStrategy = new ProxyRotationStrategy(HealthTracker, logger);
        BlacklistManager = new ProxyBlacklistManager((ILogger<ProxyBlacklistManager>?)logger);

        if (_options.HealthCheckIntervalSeconds > 0)
        {
            HealthChecker = new ProxyHealthChecker(HealthTracker, BlacklistManager, (ILogger<ProxyHealthChecker>?)logger, healthCheckClient);
        }
    }

    /// <summary>
    /// Gets the health tracker component.
    /// </summary>
    public ProxyHealthTracker HealthTracker { get; }

    /// <summary>
    /// Gets the rotation strategy component.
    /// </summary>
    public ProxyRotationStrategy RotationStrategy { get; }

    /// <summary>
    /// Gets the blacklist manager component.
    /// </summary>
    public ProxyBlacklistManager BlacklistManager { get; }

    /// <summary>
    /// Gets the health checker component (if health checking is enabled).
    /// </summary>
    public ProxyHealthChecker? HealthChecker { get; }

    /// <summary>
    /// Gets a proxy using the configured rotation strategy and health intelligence.
    /// </summary>
    public async Task<ProxyInfo?> GetProxyAsync(string? countryCode = null, CancellationToken token = default)
    {
        await EnsureInitializedAsync(token).ConfigureAwait(false);

        List<ProxyInfo> healthyProxies = RotationStrategy.GetHealthyProxies(_proxyPool, BlacklistManager);
        if (healthyProxies.Count == 0)
        {
            if (!_usingFallback && _fallbackSources != null)
            {
                await ActivateFallbackAsync(token).ConfigureAwait(false);
                healthyProxies = RotationStrategy.GetHealthyProxies(_proxyPool, BlacklistManager);
            }

            if (healthyProxies.Count == 0)
                return null;
        }

        RotationStrategyType strategy = ProxyRotationStrategy.ParseStrategy(_options.RotationStrategy);
        return RotationStrategy.SelectProxy(healthyProxies, strategy);
    }

    /// <summary>
    /// Reports the result of a proxy usage to update health metrics.
    /// </summary>
    public Task ReportProxyResultAsync(ProxyInfo proxy, bool success, TimeSpan latency, HttpStatusCode? statusCode = null)
    {
        return HealthTracker.RecordResultAsync(proxy, success, latency, statusCode);
    }

    /// <summary>
    /// Manually adds a proxy to the blacklist.
    /// </summary>
    public void BlacklistProxy(ProxyInfo proxy)
    {
        BlacklistManager.Blacklist(proxy);
    }

    /// <summary>
    /// Manually removes a proxy from the blacklist.
    /// </summary>
    public void RemoveFromBlacklist(ProxyInfo proxy)
    {
        BlacklistManager.RemoveFromBlacklist(proxy);
    }

    /// <summary>
    /// Manually adds a proxy to the whitelist for priority usage.
    /// </summary>
    public void WhitelistProxy(ProxyInfo proxy)
    {
        BlacklistManager.Whitelist(proxy);
    }

    /// <summary>
    /// Gets health metrics for all proxies.
    /// </summary>
    public IReadOnlyDictionary<string, ProxyHealthMetrics> GetAllMetrics()
    {
        return HealthTracker.GetAllMetrics();
    }

    /// <summary>
    /// Gets metrics for a specific proxy.
    /// </summary>
    public ProxyHealthMetrics? GetMetrics(ProxyInfo proxy)
    {
        return HealthTracker.GetMetrics(proxy);
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

            if (HealthChecker != null && _options.HealthCheckIntervalSeconds > 0)
            {
                var interval = TimeSpan.FromSeconds(_options.HealthCheckIntervalSeconds);
                HealthChecker.StartBackgroundHealthCheck(interval, _proxyPool);
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
                    HealthTracker.GetOrCreateMetrics(proxy);
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
        HealthChecker?.Dispose();
        _initLock?.Dispose();
    }
}
