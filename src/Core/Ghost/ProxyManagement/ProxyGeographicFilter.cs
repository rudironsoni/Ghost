using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghost.ProxyManagement;

/// <summary>
/// Provides geographic filtering and IP geolocation for proxies.
/// Uses ip-api.com free tier for geolocation (45 requests/minute).
/// </summary>
public sealed class ProxyGeographicFilter : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProxyGeographicFilter> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore;

    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Action<ILogger, string, string, string, Exception?> s_logGeolocationSuccess =
        LoggerMessage.Define<string, string, string>(LogLevel.Debug, new EventId(1, "GeolocationSuccess"),
            "Geolocated proxy {Ip} to {Country}/{City}");

    private static readonly Action<ILogger, string, Exception?> s_logGeolocationFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "GeolocationFailed"),
            "Failed to geolocate proxy {Ip}");

    public ProxyGeographicFilter(ILogger<ProxyGeographicFilter> logger)
        : this(new HttpClient { Timeout = TimeSpan.FromSeconds(10) }, logger)
    {
    }

    public ProxyGeographicFilter(HttpClient httpClient, ILogger<ProxyGeographicFilter> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Rate limit to 45 requests per minute (ip-api.com free tier limit)
        _rateLimitSemaphore = new SemaphoreSlim(45, 45);
        _ = Task.Run(async () => await RefillRateLimitAsync());
    }

    /// <summary>
    /// Filters proxies by country code (e.g., "US", "GB", "DE").
    /// </summary>
    public static IEnumerable<ProxyInfo> FilterByCountry(IEnumerable<ProxyInfo> proxies, string countryCode)
    {
        ArgumentNullException.ThrowIfNull(proxies);
        if (string.IsNullOrWhiteSpace(countryCode))
            throw new ArgumentException("Country code cannot be null or whitespace.", nameof(countryCode));

        // Note: This assumes proxies have been enriched with geolocation data
        // In practice, you'd need to call EnrichWithGeolocationAsync first
        return proxies.Where(p =>
        {
            // Extract country from proxy metadata if available
            // This is a placeholder - actual implementation would need metadata storage
            return false; // TODO: Implement metadata storage
        });
    }

    /// <summary>
    /// Filters proxies by city name.
    /// </summary>
    public static IEnumerable<ProxyInfo> FilterByCity(IEnumerable<ProxyInfo> proxies, string city)
    {
        ArgumentNullException.ThrowIfNull(proxies);
        if (string.IsNullOrWhiteSpace(city))
            throw new ArgumentException("City cannot be null or whitespace.", nameof(city));

        // Similar to FilterByCountry, requires metadata storage
        return proxies.Where(p =>
        {
            // Extract city from proxy metadata if available
            return false; // TODO: Implement metadata storage
        });
    }

    /// <summary>
    /// Enriches a proxy with geolocation data using ip-api.com.
    /// </summary>
    public async Task<ProxyGeolocation?> GetGeolocationAsync(string ipAddress, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            throw new ArgumentException("IP address cannot be null or whitespace.", nameof(ipAddress));

        // Wait for rate limit token
        await _rateLimitSemaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var url = $"http://ip-api.com/json/{ipAddress}?fields=status,message,country,countryCode,region,regionName,city,lat,lon,timezone,isp,query";
            var response = await _httpClient.GetStringAsync(url, ct).ConfigureAwait(false);

            var geolocation = JsonSerializer.Deserialize<IpApiResponse>(response, s_jsonOptions);

            if (geolocation == null || geolocation.Status != "success")
            {
                s_logGeolocationFailed(_logger, ipAddress, null);
                return null;
            }

            var result = new ProxyGeolocation
            {
                IpAddress = geolocation.Query ?? ipAddress,
                Country = geolocation.Country ?? string.Empty,
                CountryCode = geolocation.CountryCode ?? string.Empty,
                Region = geolocation.RegionName ?? string.Empty,
                City = geolocation.City ?? string.Empty,
                Latitude = geolocation.Lat,
                Longitude = geolocation.Lon,
                Timezone = geolocation.Timezone ?? string.Empty,
                Isp = geolocation.Isp ?? string.Empty
            };

            s_logGeolocationSuccess(_logger, ipAddress, result.Country, result.City, null);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            s_logGeolocationFailed(_logger, ipAddress, ex);
            return null;
        }
    }

    /// <summary>
    /// Enriches multiple proxies with geolocation data in batches.
    /// </summary>
    public async Task<Dictionary<string, ProxyGeolocation>> EnrichProxiesAsync(
        IEnumerable<ProxyInfo> proxies,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proxies);

        var results = new Dictionary<string, ProxyGeolocation>();

        foreach (var proxy in proxies)
        {
            if (ct.IsCancellationRequested)
                break;

            // Extract IP from proxy server URL
            var ip = ExtractIpFromProxyUrl(proxy.Server);
            if (string.IsNullOrEmpty(ip))
                continue;

            var geolocation = await GetGeolocationAsync(ip, ct).ConfigureAwait(false);
            if (geolocation != null)
            {
                results[proxy.Server] = geolocation;
            }

            // Add small delay to respect rate limits
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);
        }

        return results;
    }

    /// <summary>
    /// Extracts IP address from proxy URL.
    /// </summary>
    private static string? ExtractIpFromProxyUrl(string proxyUrl)
    {
        try
        {
            var uri = new Uri(proxyUrl);
            return uri.Host;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Refills the rate limit semaphore every minute.
    /// </summary>
    private async Task RefillRateLimitAsync()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(1)).ConfigureAwait(false);

            // Release tokens back to the semaphore (up to max of 45)
            var currentCount = _rateLimitSemaphore.CurrentCount;
            var tokensToAdd = 45 - currentCount;

            for (var i = 0; i < tokensToAdd; i++)
            {
                _rateLimitSemaphore.Release();
            }
        }
    }

    /// <summary>
    /// Disposes the resources used by the filter.
    /// </summary>
    public void Dispose()
    {
        _rateLimitSemaphore?.Dispose();
    }
}

/// <summary>
/// Geolocation information for a proxy.
/// </summary>
public sealed class ProxyGeolocation
{
    public string IpAddress { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Timezone { get; set; } = string.Empty;
    public string Isp { get; set; } = string.Empty;
}

/// <summary>
/// DTO for ip-api.com API response.
/// </summary>
internal sealed class IpApiResponse
{
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? Country { get; set; }
    public string? CountryCode { get; set; }
    public string? Region { get; set; }
    public string? RegionName { get; set; }
    public string? City { get; set; }
    public double Lat { get; set; }
    public double Lon { get; set; }
    public string? Timezone { get; set; }
    public string? Isp { get; set; }
    public string? Query { get; set; }
}
