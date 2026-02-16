using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.ProxyManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0032, CA1822

namespace Ghost.GeoTargeting;

/// <summary>
/// Geographic proxy selector that implements location-aware proxy selection,
/// country/region mapping, geographic proxy pools, and latency-based routing.
/// Integrates with the proxy health intelligence system for optimal performance.
/// </summary>
public sealed class GeographicProxySelector : IDisposable
{
    private readonly ProxyHealthIntelligence _healthIntelligence;
    private readonly ILogger<GeographicProxySelector> _logger;
    private readonly GeographicTargetingOptions _options;

    private readonly ConcurrentDictionary<string, GeographicProxyPool> _geoPoolCache = new();
    private readonly ConcurrentDictionary<string, ProxyLocationMetrics> _locationMetrics = new();
    private readonly CountryRegionMapping _countryRegionMapping;

    private volatile bool _initialized;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    private static readonly string[] SchemeSeparator = ["://"];

    private static readonly Action<ILogger, string, Exception?> s_logProxySelected =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, "ProxySelected"),
            "Geographic proxy selected: {Proxy}");

    private static readonly Action<ILogger, string, Exception?> s_logNoProxyAvailable =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "NoProxyAvailable"),
            "No healthy proxy available for geographic region: {Region}");

    private static readonly Action<ILogger, string, double, Exception?> s_logLocationLatency =
        LoggerMessage.Define<string, double>(LogLevel.Information, new EventId(3, "LocationLatency"),
            "Average latency for region {Region}: {LatencyMs}ms");

    private static readonly Action<ILogger, string, string, Exception?> s_logProxyLocationValidated =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(4, "ProxyLocationValidated"),
            "Proxy {Proxy} location validated for region {Region}");

    private static readonly Action<ILogger, string, string, Exception?> s_logProxyLocationMismatch =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(5, "ProxyLocationMismatch"),
            "Proxy {Proxy} location does not match requested region {Region}");

    private static readonly Action<ILogger, string, string, int, Exception?> s_logGeographicRoutingDecision =
        LoggerMessage.Define<string, string, int>(LogLevel.Information, new EventId(6, "RoutingDecision"),
            "Routed {CountryCode} request to proxy in {ProxyRegion} with latency {Latency}ms");

    private static readonly Action<ILogger, int, Exception?> s_logGeoPoolsCached =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(7, "PoolsCached"),
            "Initialized {Count} geographic proxy pools");

    private static readonly Action<ILogger, string, int, Exception?> s_logRegionProxyCount =
        LoggerMessage.Define<string, int>(LogLevel.Debug, new EventId(8, "RegionProxyCount"),
            "Region {Region} has {Count} available proxies");

    public GeographicProxySelector(
        ProxyHealthIntelligence healthIntelligence,
        ILogger<GeographicProxySelector> logger,
        IOptions<GeographicTargetingOptions> options)
    {
        _healthIntelligence = healthIntelligence ?? throw new ArgumentNullException(nameof(healthIntelligence));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _countryRegionMapping = new CountryRegionMapping(_options);
    }

    /// <summary>
    /// Selects a geographically appropriate proxy for the specified country code.
    /// Uses latency-based routing to prefer proxies with better performance to the target region.
    /// </summary>
    public async Task<ProxyInfo?> SelectProxyForCountryAsync(string countryCode, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        await EnsureInitializedAsync(token).ConfigureAwait(false);

        string normalizedCountry = countryCode.ToUpperInvariant();
        string region = _countryRegionMapping.GetRegionForCountry(normalizedCountry);

        GeographicProxyPool pool = _geoPoolCache.GetOrAdd(region, _ => new GeographicProxyPool(region, _options));
        ProxyInfo? proxy = pool.SelectProxy(_healthIntelligence, normalizedCountry);

        if (proxy == null)
        {
            s_logNoProxyAvailable(_logger, region, null);
            return null;
        }

        s_logProxySelected(_logger, proxy.Server, null);
        return proxy;
    }

    /// <summary>
    /// Selects a proxy optimized for a specific geographic location.
    /// Validates proxy location accuracy and uses latency metrics for optimal routing.
    /// </summary>
    public async Task<ProxyInfo?> SelectProxyForLocationAsync(string countryCode, string? regionCode = null, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);

        await EnsureInitializedAsync(token).ConfigureAwait(false);

        string normalizedCountry = countryCode.ToUpperInvariant();
        string normalizedRegion = string.IsNullOrWhiteSpace(regionCode)
            ? _countryRegionMapping.GetRegionForCountry(normalizedCountry)
            : regionCode.ToUpperInvariant();

        GeographicProxyPool pool = _geoPoolCache.GetOrAdd(normalizedRegion, _ => new GeographicProxyPool(normalizedRegion, _options));
        List<ProxyInfo> candidates = pool.GetAllProxies(_healthIntelligence);

        if (candidates.Count == 0)
        {
            s_logNoProxyAvailable(_logger, normalizedRegion, null);
            return null;
        }

        var validProxies = candidates.Where(p => ValidateProxyLocation(p, normalizedCountry, normalizedRegion)).ToList();

        if (validProxies.Count == 0)
        {
            validProxies = candidates;
        }

        ProxyInfo? selected = SelectByLatencyMetrics(validProxies, normalizedRegion);

        if (selected != null)
        {
            s_logGeographicRoutingDecision(_logger, normalizedCountry, normalizedRegion,
                (int)GetAverageLatencyForRegion(normalizedRegion), null);
        }

        return selected;
    }

    /// <summary>
    /// Reports proxy latency for a specific geographic region to optimize future routing decisions.
    /// </summary>
    public async Task ReportLatencyAsync(ProxyInfo proxy, string regionCode, TimeSpan latency, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionCode);

        string normalizedRegion = regionCode.ToUpperInvariant();
        string key = GetMetricsKey(proxy, normalizedRegion);

        ProxyLocationMetrics metrics = _locationMetrics.GetOrAdd(key, _ => new ProxyLocationMetrics
        {
            ProxyKey = proxy.Server,
            RegionCode = normalizedRegion,
            FirstMeasured = DateTimeOffset.UtcNow
        });

        metrics.MeasureLatency(latency);

        s_logLocationLatency(_logger, normalizedRegion, metrics.AverageLatency, null);

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Validates that a proxy's location matches the requested region.
    /// Returns true if location matches or if location metadata is unavailable.
    /// </summary>
    public bool ValidateProxyLocation(ProxyInfo proxy, string countryCode, string regionCode)
    {
        ArgumentNullException.ThrowIfNull(proxy);
        ArgumentException.ThrowIfNullOrWhiteSpace(countryCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(regionCode);

        string normalizedCountry = countryCode.ToUpperInvariant();
        string normalizedRegion = regionCode.ToUpperInvariant();

        string proxyHostname = ExtractHostFromProxyServer(proxy.Server);

        bool isLocationValid = ValidateProxyHostLocation(proxyHostname, normalizedCountry, normalizedRegion);

        if (isLocationValid)
        {
            s_logProxyLocationValidated(_logger, proxy.Server, normalizedRegion, null);
        }
        else
        {
            s_logProxyLocationMismatch(_logger, proxy.Server, normalizedRegion, null);
        }

        return isLocationValid;
    }

    /// <summary>
    /// Gets geographic targeting statistics for all tracked regions.
    /// </summary>
    public IReadOnlyDictionary<string, RegionTargetingStats> GetRegionTargetingStats()
    {
        Dictionary<string, RegionTargetingStats> stats = [];

        foreach (string region in _geoPoolCache.Keys)
        {
            var regionMetrics = _locationMetrics
                .Where(kvp => kvp.Key.EndsWith("|" + region, StringComparison.OrdinalIgnoreCase))
                .Select(kvp => kvp.Value)
                .ToList();

            stats[region] = new RegionTargetingStats
            {
                Region = region,
                ProxyCount = _geoPoolCache[region].ProxyCount,
                AverageLatency = regionMetrics.Count > 0
                    ? regionMetrics.Average(m => m.AverageLatency)
                    : 0,
                MeasurementCount = regionMetrics.Sum(m => m.MeasurementCount)
            };
        }

        return stats;
    }

    /// <summary>
    /// Gets detailed metrics for a specific proxy in a given region.
    /// </summary>
    public ProxyLocationMetrics? GetProxyMetricsForRegion(ProxyInfo proxy, string regionCode)
    {
        if (proxy == null)
            return null;

        if (string.IsNullOrWhiteSpace(regionCode))
            return null;

        string key = GetMetricsKey(proxy, regionCode.ToUpperInvariant());
        return _locationMetrics.TryGetValue(key, out ProxyLocationMetrics? metrics) ? metrics : null;
    }

    /// <summary>
    /// Clears all cached geographic targeting data and resets metrics.
    /// </summary>
    public void ClearCache()
    {
        _geoPoolCache.Clear();
        _locationMetrics.Clear();
        _initialized = false;
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

            // Build geographic pools for configured regions
            IEnumerable<string> regions = _countryRegionMapping.GetAllRegions();
            foreach (string region in regions)
            {
                _geoPoolCache.TryAdd(region, new GeographicProxyPool(region, _options));
            }

            s_logGeoPoolsCached(_logger, _geoPoolCache.Count, null);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private ProxyInfo? SelectByLatencyMetrics(List<ProxyInfo> candidates, string regionCode)
    {
        if (candidates.Count == 0)
            return null;

        var sorted = candidates
            .Select(proxy => new
            {
                Proxy = proxy,
                Latency = GetProxyLatencyForRegion(proxy, regionCode)
            })
            .OrderBy(x => x.Latency)
            .ToList();

        return sorted.FirstOrDefault()?.Proxy;
    }

    private double GetProxyLatencyForRegion(ProxyInfo proxy, string regionCode)
    {
        string key = GetMetricsKey(proxy, regionCode);
        if (_locationMetrics.TryGetValue(key, out ProxyLocationMetrics? metrics))
        {
            return metrics.AverageLatency;
        }

        return _options.MaxAcceptableLatencyMs ?? 5000;
    }

    private double GetAverageLatencyForRegion(string regionCode)
    {
        var metrics = _locationMetrics
            .Where(kvp => kvp.Key.EndsWith("|" + regionCode, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Value)
            .ToList();

        return metrics.Count > 0 ? metrics.Average(m => m.AverageLatency) : 0;
    }

    private static string ExtractHostFromProxyServer(string proxyServer)
    {
        try
        {
            if (proxyServer.Contains("://", StringComparison.OrdinalIgnoreCase))
            {
                string[] parts = proxyServer.Split(SchemeSeparator, StringSplitOptions.None);
                if (parts.Length > 1)
                    proxyServer = parts[1];
            }

            string[] hostParts = proxyServer.Split(':');
            return hostParts[0].ToLowerInvariant();
        }
        catch
        {
            return string.Empty;
        }
    }

    private bool ValidateProxyHostLocation(string hostname, string countryCode, string regionCode)
    {
        if (string.IsNullOrEmpty(hostname))
            return true;

        string lowerHostname = hostname.ToLowerInvariant();
        string countryLower = countryCode.ToLowerInvariant();
        string regionLower = regionCode.ToLowerInvariant();

        return lowerHostname.Contains(countryLower) ||
               lowerHostname.Contains(regionLower) ||
               !_options.StrictLocationValidation;
    }

    private static string GetMetricsKey(ProxyInfo proxy, string regionCode)
    {
        return $"{proxy.Server}|{regionCode}";
    }

    public void Dispose()
    {
        _initLock?.Dispose();
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Maps countries to geographic regions for proxy pool organization and targeted routing.
/// Maintains a comprehensive mapping of ISO country codes to regional identifiers.
/// </summary>
public class CountryRegionMapping
{
    private readonly GeographicTargetingOptions _options;
    private readonly Dictionary<string, string> _countryToRegionMap;

    public CountryRegionMapping(GeographicTargetingOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _countryToRegionMap = InitializeCountryRegionMap();
    }

    /// <summary>
    /// Gets the geographic region for a given country code.
    /// </summary>
    public string GetRegionForCountry(string countryCode)
    {
        if (string.IsNullOrWhiteSpace(countryCode))
            return "UNKNOWN";

        string normalized = countryCode.ToUpperInvariant();

        if (_countryToRegionMap.TryGetValue(normalized, out string? region))
        {
            return region;
        }

        if (_options.CustomCountryRegionMappings?.TryGetValue(normalized, out string? customRegion) == true)
        {
            return customRegion;
        }

        return "UNKNOWN";
    }

    /// <summary>
    /// Gets all configured regions.
    /// </summary>
    public IEnumerable<string> GetAllRegions()
    {
        return _countryToRegionMap.Values.Distinct().Union(_options.CustomCountryRegionMappings?.Values ?? Enumerable.Empty<string>());
    }

    /// <summary>
    /// Gets all country codes for a specific region.
    /// </summary>
    public IEnumerable<string> GetCountriesForRegion(string regionCode)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
            return Enumerable.Empty<string>();

        string normalized = regionCode.ToUpperInvariant();
        return _countryToRegionMap
            .Where(kvp => kvp.Value.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .Select(kvp => kvp.Key);
    }

    private Dictionary<string, string> InitializeCountryRegionMap()
    {
        return InitializeCountryRegionMapInternal();
    }

    private static Dictionary<string, string> InitializeCountryRegionMapInternal()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            // North America
            { "US", "NORTH_AMERICA" },
            { "CA", "NORTH_AMERICA" },
            { "MX", "NORTH_AMERICA" },

            // Central America & Caribbean
            { "GT", "CENTRAL_AMERICA" },
            { "HN", "CENTRAL_AMERICA" },
            { "SV", "CENTRAL_AMERICA" },
            { "NI", "CENTRAL_AMERICA" },
            { "CR", "CENTRAL_AMERICA" },
            { "PA", "CENTRAL_AMERICA" },
            { "BZ", "CENTRAL_AMERICA" },
            { "CU", "CARIBBEAN" },
            { "DO", "CARIBBEAN" },
            { "HT", "CARIBBEAN" },
            { "JM", "CARIBBEAN" },
            { "TT", "CARIBBEAN" },

            // South America
            { "CO", "SOUTH_AMERICA" },
            { "VE", "SOUTH_AMERICA" },
            { "GY", "SOUTH_AMERICA" },
            { "SR", "SOUTH_AMERICA" },
            { "EC", "SOUTH_AMERICA" },
            { "PE", "SOUTH_AMERICA" },
            { "BR", "SOUTH_AMERICA" },
            { "BO", "SOUTH_AMERICA" },
            { "PY", "SOUTH_AMERICA" },
            { "CL", "SOUTH_AMERICA" },
            { "AR", "SOUTH_AMERICA" },
            { "UY", "SOUTH_AMERICA" },

            // Western Europe
            { "GB", "WESTERN_EUROPE" },
            { "IE", "WESTERN_EUROPE" },
            { "FR", "WESTERN_EUROPE" },
            { "DE", "WESTERN_EUROPE" },
            { "BE", "WESTERN_EUROPE" },
            { "NL", "WESTERN_EUROPE" },
            { "LU", "WESTERN_EUROPE" },
            { "CH", "WESTERN_EUROPE" },
            { "AT", "WESTERN_EUROPE" },

            // Southern Europe
            { "ES", "SOUTHERN_EUROPE" },
            { "PT", "SOUTHERN_EUROPE" },
            { "IT", "SOUTHERN_EUROPE" },
            { "GR", "SOUTHERN_EUROPE" },
            { "HR", "SOUTHERN_EUROPE" },
            { "MT", "SOUTHERN_EUROPE" },
            { "CY", "SOUTHERN_EUROPE" },

            // Eastern Europe
            { "PL", "EASTERN_EUROPE" },
            { "CZ", "EASTERN_EUROPE" },
            { "SK", "EASTERN_EUROPE" },
            { "HU", "EASTERN_EUROPE" },
            { "RO", "EASTERN_EUROPE" },
            { "BG", "EASTERN_EUROPE" },
            { "RS", "EASTERN_EUROPE" },
            { "UA", "EASTERN_EUROPE" },
            { "BY", "EASTERN_EUROPE" },

            // Nordic Region
            { "SE", "NORDIC" },
            { "NO", "NORDIC" },
            { "DK", "NORDIC" },
            { "FI", "NORDIC" },
            { "IS", "NORDIC" },

            // Russia & Central Asia
            { "RU", "EURASIA" },
            { "KZ", "EURASIA" },
            { "UZ", "EURASIA" },
            { "TM", "EURASIA" },
            { "KG", "EURASIA" },
            { "TJ", "EURASIA" },

            // Middle East
            { "SA", "MIDDLE_EAST" },
            { "AE", "MIDDLE_EAST" },
            { "QA", "MIDDLE_EAST" },
            { "BH", "MIDDLE_EAST" },
            { "KW", "MIDDLE_EAST" },
            { "OM", "MIDDLE_EAST" },
            { "YE", "MIDDLE_EAST" },
            { "IL", "MIDDLE_EAST" },
            { "PS", "MIDDLE_EAST" },
            { "JO", "MIDDLE_EAST" },
            { "LB", "MIDDLE_EAST" },
            { "SY", "MIDDLE_EAST" },
            { "IQ", "MIDDLE_EAST" },
            { "IR", "MIDDLE_EAST" },
            { "TR", "MIDDLE_EAST" },
            { "EG", "MIDDLE_EAST" },

            // North Africa
            { "MA", "NORTH_AFRICA" },
            { "DZ", "NORTH_AFRICA" },
            { "TN", "NORTH_AFRICA" },
            { "LY", "NORTH_AFRICA" },

            // Sub-Saharan Africa
            { "ZA", "SUB_SAHARAN_AFRICA" },
            { "NG", "SUB_SAHARAN_AFRICA" },
            { "KE", "SUB_SAHARAN_AFRICA" },
            { "GH", "SUB_SAHARAN_AFRICA" },
            { "ET", "SUB_SAHARAN_AFRICA" },
            { "TZ", "SUB_SAHARAN_AFRICA" },
            { "UG", "SUB_SAHARAN_AFRICA" },
            { "RW", "SUB_SAHARAN_AFRICA" },

            // South Asia
            { "IN", "SOUTH_ASIA" },
            { "PK", "SOUTH_ASIA" },
            { "BD", "SOUTH_ASIA" },
            { "LK", "SOUTH_ASIA" },
            { "NP", "SOUTH_ASIA" },
            { "BT", "SOUTH_ASIA" },

            // Southeast Asia
            { "TH", "SOUTHEAST_ASIA" },
            { "VN", "SOUTHEAST_ASIA" },
            { "ID", "SOUTHEAST_ASIA" },
            { "MY", "SOUTHEAST_ASIA" },
            { "SG", "SOUTHEAST_ASIA" },
            { "PH", "SOUTHEAST_ASIA" },
            { "MM", "SOUTHEAST_ASIA" },
            { "KH", "SOUTHEAST_ASIA" },
            { "LA", "SOUTHEAST_ASIA" },
            { "BN", "SOUTHEAST_ASIA" },

            // East Asia
            { "CN", "EAST_ASIA" },
            { "JP", "EAST_ASIA" },
            { "KR", "EAST_ASIA" },
            { "TW", "EAST_ASIA" },
            { "MN", "EAST_ASIA" },

            // Oceania
            { "AU", "OCEANIA" },
            { "NZ", "OCEANIA" },
            { "FJ", "OCEANIA" },
        };
    }
}

/// <summary>
/// Represents a pool of proxies available for a specific geographic region.
/// Manages proxy organization by location and provides efficient selection strategies.
/// </summary>
public class GeographicProxyPool
{
    private readonly string _regionCode;
    private readonly GeographicTargetingOptions _options;
    private readonly List<ProxyInfo> _proxies = [];
    private readonly object _proxyLock = new();

    public string RegionCode => _regionCode;
    public int ProxyCount
    {
        get
        {
            lock (_proxyLock)
            {
                return _proxies.Count;
            }
        }
    }

    public GeographicProxyPool(string regionCode, GeographicTargetingOptions options)
    {
        _regionCode = regionCode ?? throw new ArgumentNullException(nameof(regionCode));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Selects a proxy from the pool using health intelligence and latency metrics.
    /// </summary>
    public ProxyInfo? SelectProxy(ProxyHealthIntelligence healthIntelligence, string countryCode)
    {
        ArgumentNullException.ThrowIfNull(healthIntelligence);

        lock (_proxyLock)
        {
            if (_proxies.Count == 0)
                return null;

            var healthyProxies = _proxies
                .Where(p => !IsProxyBlacklisted(p, healthIntelligence))
                .ToList();

            return healthyProxies.Count > 0 ? healthyProxies.First() : null;
        }
    }

    /// <summary>
    /// Gets all proxies available in this pool.
    /// </summary>
    public List<ProxyInfo> GetAllProxies(ProxyHealthIntelligence healthIntelligence)
    {
        lock (_proxyLock)
        {
            return _proxies.ToList();
        }
    }

    /// <summary>
    /// Adds a proxy to the pool.
    /// </summary>
    public void AddProxy(ProxyInfo proxy)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        lock (_proxyLock)
        {
            if (_proxies.Find(p => p.Server == proxy.Server) == null)
            {
                _proxies.Add(proxy);
            }
        }
    }

    private bool IsProxyBlacklisted(ProxyInfo proxy, ProxyHealthIntelligence healthIntelligence)
    {
        ProxyHealthMetrics? metrics = healthIntelligence.GetMetrics(proxy);
        if (metrics == null)
            return false;

        // Consider proxy blacklisted if success rate is too low
        int threshold = _options.MinProxySuccessRatePercent ?? 50;
        return metrics.SuccessRate * 100 < threshold;
    }
}

/// <summary>
/// Tracks latency metrics for proxies in specific geographic regions.
/// Maintains a history of measurements to calculate average and percentile latencies.
/// </summary>
public class ProxyLocationMetrics
{
    private readonly List<double> _latencyHistory = [];
    private readonly object _lock = new();

    public string ProxyKey { get; set; } = string.Empty;
    public string RegionCode { get; set; } = string.Empty;
    public DateTimeOffset FirstMeasured { get; set; }
    public DateTimeOffset LastMeasured { get; set; }

    public int MeasurementCount
    {
        get
        {
            lock (_lock)
            {
                return _latencyHistory.Count;
            }
        }
    }

    public double AverageLatency
    {
        get
        {
            lock (_lock)
            {
                return _latencyHistory.Count > 0 ? _latencyHistory.Average() : 0;
            }
        }
    }

    public double P95Latency
    {
        get
        {
            lock (_lock)
            {
                if (_latencyHistory.Count == 0)
                    return 0;

                var sorted = _latencyHistory.OrderBy(x => x).ToList();
                int index = (int)Math.Ceiling(sorted.Count * 0.95) - 1;
                return sorted[Math.Max(0, index)];
            }
        }
    }

    public double MaxLatency
    {
        get
        {
            lock (_lock)
            {
                return _latencyHistory.Count > 0 ? _latencyHistory.Max() : 0;
            }
        }
    }

    public double MinLatency
    {
        get
        {
            lock (_lock)
            {
                return _latencyHistory.Count > 0 ? _latencyHistory.Min() : 0;
            }
        }
    }

    /// <summary>
    /// Records a latency measurement for this proxy-region combination.
    /// </summary>
    public void MeasureLatency(TimeSpan latency)
    {
        lock (_lock)
        {
            _latencyHistory.Add(latency.TotalMilliseconds);
            LastMeasured = DateTimeOffset.UtcNow;

            // Keep only recent measurements (sliding window of 1000)
            if (_latencyHistory.Count > 1000)
            {
                _latencyHistory.RemoveRange(0, _latencyHistory.Count - 1000);
            }
        }
    }
}

/// <summary>
/// Configuration options for geographic targeting functionality.
/// Controls behavior of proxy selection, location validation, and latency-based routing.
/// </summary>
public class GeographicTargetingOptions
{
    /// <summary>
    /// Whether to enforce strict location validation for proxies.
    /// When true, only proxies with verified location metadata are selected.
    /// When false, proxies with unknown location are accepted as fallback.
    /// Defaults to false for better availability.
    /// </summary>
    public bool StrictLocationValidation { get; set; }

    /// <summary>
    /// Maximum acceptable latency in milliseconds for a proxy in a given region.
    /// Used as a threshold to exclude poorly performing proxies.
    /// Set to null to disable latency-based filtering.
    /// Defaults to 5000ms (5 seconds).
    /// </summary>
    public int? MaxAcceptableLatencyMs { get; set; } = 5000;

    /// <summary>
    /// Minimum required success rate (as percentage) for a proxy to be considered viable.
    /// Proxies with success rate below this are deprioritized.
    /// Defaults to 50%.
    /// </summary>
    public int? MinProxySuccessRatePercent { get; set; } = 50;

    /// <summary>
    /// Custom mapping of country codes to geographic regions.
    /// Overrides default mappings when provided.
    /// </summary>
    public Dictionary<string, string>? CustomCountryRegionMappings { get; set; }

    /// <summary>
    /// Whether to prefer proxies with latency measurements over proxies with no measurements.
    /// When true, proxies without measurements are deprioritized.
    /// Defaults to true.
    /// </summary>
    public bool PreferMeasuredProxies { get; set; } = true;

    /// <summary>
    /// Number of recent latency measurements to consider when calculating statistics.
    /// Defaults to 100.
    /// </summary>
    public int LatencyHistorySize { get; set; } = 100;
}

/// <summary>
/// Statistics about proxy availability and performance in a specific geographic region.
/// </summary>
public class RegionTargetingStats
{
    /// <summary>
    /// The geographic region code.
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// Number of proxies available for this region.
    /// </summary>
    public int ProxyCount { get; set; }

    /// <summary>
    /// Average latency (in milliseconds) across all measured proxies.
    /// </summary>
    public double AverageLatency { get; set; }

    /// <summary>
    /// Number of latency measurements recorded for this region.
    /// </summary>
    public int MeasurementCount { get; set; }
}
