using System;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;

namespace Ghost.ProxyManagement;

/// <summary>
/// Tests free proxy health using httpbin.org/ip endpoint.
/// Validates response time (must be &lt; 5 seconds) and tracks success rate.
/// Removes proxies with &lt; 80% success rate.
/// </summary>
public sealed class FreeProxyHealthChecker
{
    private readonly ILogger<FreeProxyHealthChecker> _logger;
    private readonly TimeSpan _timeout;
    private readonly double _minimumSuccessRate;

    private static readonly Action<ILogger, string, double, Exception?> s_logHealthCheckPassed =
        LoggerMessage.Define<string, double>(LogLevel.Debug, new EventId(1, "HealthCheckPassed"),
            "Proxy {Server} health check passed in {ResponseTime}ms");

    private static readonly Action<ILogger, string, Exception?> s_logHealthCheckFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, "HealthCheckFailed"),
            "Proxy {Server} health check failed");

    public FreeProxyHealthChecker(ILogger<FreeProxyHealthChecker> logger)
        : this(logger, TimeSpan.FromSeconds(5), 0.8)
    {
    }

    public FreeProxyHealthChecker(ILogger<FreeProxyHealthChecker> logger, TimeSpan timeout, double minimumSuccessRate)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeout = timeout;
        _minimumSuccessRate = minimumSuccessRate;
    }

    /// <summary>
    /// Performs a health check on the given proxy.
    /// </summary>
    /// <returns>Health check result with response time and success status</returns>
    public async Task<ProxyHealthCheckResult> CheckHealthAsync(ProxyInfo proxy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(proxy);

        var stopwatch = Stopwatch.StartNew();
        var result = new ProxyHealthCheckResult
        {
            Proxy = proxy,
            CheckedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var proxyUri = new Uri(proxy.Server);
            var webProxy = new WebProxy(proxyUri);

            if (!string.IsNullOrEmpty(proxy.Username))
            {
                webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
            }

            using var handler = new HttpClientHandler
            {
                Proxy = webProxy,
                UseProxy = true
            };

            using var client = new HttpClient(handler)
            {
                Timeout = _timeout
            };

            HttpResponseMessage response = await client.GetAsync("https://httpbin.org/ip", ct).ConfigureAwait(false);
            stopwatch.Stop();

            result.ResponseTime = stopwatch.Elapsed;
            result.IsHealthy = response.IsSuccessStatusCode && stopwatch.Elapsed < _timeout;
            result.StatusCode = response.StatusCode;

            if (result.IsHealthy)
            {
                s_logHealthCheckPassed(_logger, proxy.Server, stopwatch.Elapsed.TotalMilliseconds, null);
            }
            else
            {
                s_logHealthCheckFailed(_logger, proxy.Server, null);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            result.ResponseTime = stopwatch.Elapsed;
            result.IsHealthy = false;
            result.ErrorMessage = ex.Message;
            s_logHealthCheckFailed(_logger, proxy.Server, ex);
        }

        return result;
    }

    /// <summary>
    /// Checks if a proxy should be removed based on its success rate.
    /// </summary>
    public bool ShouldRemoveProxy(long totalRequests, long successfulRequests)
    {
        if (totalRequests == 0)
            return false;

        double successRate = (double)successfulRequests / totalRequests;
        return successRate < _minimumSuccessRate;
    }

    /// <summary>
    /// Calculates the success rate for the given metrics.
    /// </summary>
    public static double CalculateSuccessRate(long totalRequests, long successfulRequests)
    {
        if (totalRequests == 0)
            return 0.0;

        return (double)successfulRequests / totalRequests;
    }
}

/// <summary>
/// Result of a proxy health check.
/// </summary>
public sealed class ProxyHealthCheckResult
{
    public ProxyInfo Proxy { get; set; } = null!;
    public bool IsHealthy { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public HttpStatusCode? StatusCode { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CheckedAt { get; set; }
}
