using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Kernel;
using Ghost.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Proxy;

/// <summary>
/// Validates NordVPN proxy endpoints and measures their health and latency.
/// </summary>
public sealed class ProxyHealthChecker
{
    private const int HealthyLatencyThresholdMs = 1000;
    private static readonly TimeSpan s_defaultTimeout = TimeSpan.FromSeconds(10);
    private static readonly Uri s_healthCheckUri = new("https://httpbin.org/ip");

    private readonly HttpClient _httpClient;
    private readonly ILogger<ProxyHealthChecker> _logger;

    private static readonly string[] s_nordVpnProxies =
    {
        "socks5://nl.socks.nordhold.net:1080",
        "socks5://se.socks.nordhold.net:1080",
        "socks5://us.socks.nordhold.net:1080",
        "socks5://amsterdam.nl.socks.nordhold.net:1080",
        "socks5://atlanta.us.socks.nordhold.net:1080",
        "socks5://chicago.us.socks.nordhold.net:1080",
        "socks5://dallas.us.socks.nordhold.net:1080",
        "socks5://los-angeles.us.socks.nordhold.net:1080",
        "socks5://new-york.us.socks.nordhold.net:1080",
        "socks5://phoenix.us.socks.nordhold.net:1080",
        "socks5://san-francisco.us.socks.nordhold.net:1080",
        "socks5://stockholm.se.socks.nordhold.net:1080"
    };

    private static readonly Action<ILogger, string, Exception?> s_logProxyCheckStarted =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(ProxyHealthChecker)), "Checking proxy {Proxy}");

    private static readonly Action<ILogger, string, long, Exception?> s_logProxyCheckSucceeded =
        LoggerMessage.Define<string, long>(LogLevel.Information, new EventId(2, nameof(ProxyHealthChecker)), "Proxy {Proxy} healthy with latency {Latency}ms");

    private static readonly Action<ILogger, string, string, Exception?> s_logProxyCheckFailed =
        LoggerMessage.Define<string, string>(LogLevel.Warning, new EventId(3, nameof(ProxyHealthChecker)), "Proxy {Proxy} failed health check: {Error}");

    /// <summary>
    /// Initializes a new instance of the <see cref="ProxyHealthChecker"/> class.
    /// </summary>
    public ProxyHealthChecker(HttpClient httpClient, ILogger<ProxyHealthChecker>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _logger = logger ?? NullLogger<ProxyHealthChecker>.Instance;
    }

    /// <summary>
    /// Checks all known NordVPN proxies and returns a consolidated health report.
    /// </summary>
    public async Task<ProxyHealthReport> CheckAllProxiesAsync(CancellationToken cancellationToken = default)
    {
        string? username = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME");
        string? password = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            return new ProxyHealthReport
            {
                Proxies = s_nordVpnProxies
                    .Select(proxy => new ProxyStatus
                    {
                        Url = proxy,
                        IsHealthy = false,
                        LatencyMs = -1,
                        Error = "NordVPN credentials are missing.",
                        LastChecked = DateTime.UtcNow
                    })
                    .ToList(),
                HealthyCount = 0,
                UnhealthyCount = s_nordVpnProxies.Length
            };
        }

        Task<ProxyStatus>[] statusTasks = s_nordVpnProxies
            .Select(proxy => CheckProxyAsync(proxy, username, password, cancellationToken))
            .ToArray();

        ProxyStatus[] statuses = await Task.WhenAll(statusTasks).ConfigureAwait(false);
        int healthyCount = statuses.Count(status => status.IsHealthy);

        return new ProxyHealthReport
        {
            Proxies = statuses.ToList(),
            HealthyCount = healthyCount,
            UnhealthyCount = statuses.Length - healthyCount
        };
    }

    /// <summary>
    /// Measures latency for a proxy URL by issuing a health check request.
    /// </summary>
    public async Task<long> MeasureLatencyAsync(string proxyUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(proxyUrl))
            throw new ArgumentException("Proxy URL is required.", nameof(proxyUrl));

        string? username = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_USERNAME");
        string? password = Environment.GetEnvironmentVariable("DOTNET_GHOST_NORDVPN_PASSWORD");

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return -1;

        ProxyStatus result = await CheckProxyAsync(proxyUrl, username, password, cancellationToken).ConfigureAwait(false);
        return result.LatencyMs;
    }

    private async Task<ProxyStatus> CheckProxyAsync(string proxyUrl, string? username, string? password, CancellationToken cancellationToken)
    {
        var status = new ProxyStatus
        {
            Url = proxyUrl,
            IsHealthy = false,
            LatencyMs = -1,
            Error = string.Empty,
            LastChecked = DateTime.UtcNow
        };

        if (string.IsNullOrWhiteSpace(proxyUrl))
        {
            status.Error = "Proxy URL is empty.";
            return status;
        }

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            status.Error = "NordVPN credentials are missing.";
            return status;
        }

        s_logProxyCheckStarted(_logger, proxyUrl, null);

        try
        {
            var proxySource = new StaticProxySource(new ProxySourceConfig
            {
                Enabled = true,
                Username = username,
                Password = password,
                Hosts = { proxyUrl }
            }, NullLogger<StaticProxySource>.Instance);

            ProxyInfo? proxyInfo = (await proxySource.FetchProxiesAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault();
            if (proxyInfo == null)
            {
                status.Error = "Proxy configuration could not be parsed.";
                return status;
            }

            using var handler = new HttpClientHandler
            {
                Proxy = BuildWebProxy(proxyInfo),
                UseProxy = true
            };

            using var client = new HttpClient(handler)
            {
                Timeout = s_defaultTimeout
            };

            var stopwatch = Stopwatch.StartNew();
            using HttpResponseMessage response = await client.GetAsync(s_healthCheckUri, cancellationToken).ConfigureAwait(false);
            stopwatch.Stop();

            status.LatencyMs = stopwatch.ElapsedMilliseconds;
            status.LastChecked = DateTime.UtcNow;

            if (response.IsSuccessStatusCode && status.LatencyMs < HealthyLatencyThresholdMs)
            {
                status.IsHealthy = true;
                s_logProxyCheckSucceeded(_logger, proxyUrl, status.LatencyMs, null);
                return status;
            }

            status.IsHealthy = false;
            status.Error = response.IsSuccessStatusCode
                ? $"Latency exceeded {HealthyLatencyThresholdMs}ms."
                : $"Health check failed with status {(int)response.StatusCode}.";

            s_logProxyCheckFailed(_logger, proxyUrl, status.Error, null);
            return status;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            status.Error = "Health check cancelled.";
            return status;
        }
        catch (Exception ex)
        {
            status.Error = ex.Message;
            s_logProxyCheckFailed(_logger, proxyUrl, status.Error, ex);
            return status;
        }
    }

    private static WebProxy BuildWebProxy(ProxyInfo proxyInfo)
    {
        var uri = new Uri(proxyInfo.Server);
        var webProxy = new WebProxy(uri);
        if (!string.IsNullOrEmpty(proxyInfo.Username))
        {
            webProxy.Credentials = new NetworkCredential(proxyInfo.Username, proxyInfo.Password);
        }

        return webProxy;
    }
}
