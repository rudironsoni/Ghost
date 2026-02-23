using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Ghost.ProxyConfiguration;
using Microsoft.Extensions.Logging;

namespace Ghost.ProxyManagement;

/// <summary>
/// Performs health checks on proxy servers.
/// Single responsibility: Checks proxy health via HTTP requests.
/// </summary>
public sealed class ProxyHealthChecker : IDisposable
{
    private readonly ProxyHealthTracker _healthTracker;
    private readonly ProxyBlacklistManager _blacklistManager;
    private readonly ILogger? _logger;
    private readonly HttpClient _healthCheckClient;
    private readonly CancellationTokenSource _healthCheckCts = new();
    private readonly TimeProvider _timeProvider;
    private Task? _healthCheckTask;
    private bool _disposed;

    private static readonly Action<ILogger, Exception?> s_logHealthCheckStarted =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, "HealthCheckStarted"), "Background proxy health checking started");

    private static readonly Action<ILogger, Exception?> s_logHealthCheckStopped =
        LoggerMessage.Define(LogLevel.Information, new EventId(2, "HealthCheckStopped"), "Background proxy health checking stopped");

    private static readonly Action<ILogger, string, double, Exception?> s_logProxyHealthy =
        LoggerMessage.Define<string, double>(LogLevel.Debug, new EventId(3, "ProxyHealthy"), "Proxy {Proxy} health check passed in {Latency}ms");

    private static readonly Action<ILogger, string, Exception?> s_logProxyUnhealthy =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, "ProxyUnhealthy"), "Proxy {Proxy} health check failed");

    private static readonly Action<ILogger, Exception?> s_logHealthCheckCycleFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(5, "HealthCheckCycleFailed"), "Error during background health check cycle");

    public ProxyHealthChecker(
        ProxyHealthTracker healthTracker,
        ProxyBlacklistManager blacklistManager,
        ILogger? logger,
        HttpClient? healthCheckClient = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(healthTracker);
        ArgumentNullException.ThrowIfNull(blacklistManager);
        _healthTracker = healthTracker;
        _blacklistManager = blacklistManager;
        _logger = logger;
        _healthCheckClient = healthCheckClient ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Starts background health checking with the specified interval.
    /// </summary>
    public void StartBackgroundHealthCheck(TimeSpan interval, IEnumerable<KeyValuePair<string, ProxyInfo>> proxyPool)
    {
        if (_healthCheckTask != null)
            return;

        if (_logger != null)
        {
            s_logHealthCheckStarted(_logger, null);
        }

        _healthCheckTask = Task.Run(async () =>
            await PerformBackgroundHealthCheckAsync(interval, proxyPool, _healthCheckCts.Token).ConfigureAwait(false),
            _healthCheckCts.Token);
    }

    /// <summary>
    /// Stops background health checking.
    /// </summary>
    public async Task StopBackgroundHealthCheckAsync()
    {
        if (_healthCheckTask == null)
            return;

        try
        {
            _healthCheckCts.Cancel();
            await _healthCheckTask.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Expected when cancellation is requested
        }
        catch (TimeoutException)
        {
            // Task didn't complete in time
        }
        catch (Exception)
        {
            // Ignore other exceptions during shutdown
        }
        finally
        {
            _healthCheckTask = null;
        }

        if (_logger != null)
        {
            s_logHealthCheckStopped(_logger, null);
        }
    }

    /// <summary>
    /// Performs a single health check on a specific proxy.
    /// </summary>
    public async Task<HealthCheckResult> CheckProxyHealthAsync(ProxyInfo proxy, CancellationToken token = default)
    {
        if (proxy == null)
            return new HealthCheckResult { Success = false, Error = "Proxy is null" };

        if (_blacklistManager.IsBlacklisted(proxy))
            return new HealthCheckResult { Success = false, Error = "Proxy is blacklisted" };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var proxyUri = new Uri(proxy.Server);
            var webProxy = new WebProxy(proxyUri);

            if (!string.IsNullOrEmpty(proxy.Username))
            {
                webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
            }

            using var handler = new HttpClientHandler { Proxy = webProxy };
            using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };

            HttpResponseMessage response = await client.GetAsync("https://httpbin.org/ip", token).ConfigureAwait(false);
            stopwatch.Stop();

            bool success = response.IsSuccessStatusCode;
            await _healthTracker.RecordResultAsync(proxy, success, stopwatch.Elapsed, response.StatusCode).ConfigureAwait(false);

            if (success)
            {
                if (_logger != null)
                {
                    s_logProxyHealthy(_logger, proxy.Server, stopwatch.Elapsed.TotalMilliseconds, null);
                }
            }
            else
            {
                if (_logger != null)
                {
                    s_logProxyUnhealthy(_logger, proxy.Server, null);
                }
            }

            return new HealthCheckResult
            {
                Success = success,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds,
                StatusCode = (int)response.StatusCode
            };
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            await _healthTracker.RecordResultAsync(proxy, false, stopwatch.Elapsed).ConfigureAwait(false);

            if (_logger != null)
            {
                s_logProxyUnhealthy(_logger, proxy.Server, ex);
            }

            return new HealthCheckResult
            {
                Success = false,
                Error = ex.Message,
                LatencyMs = stopwatch.Elapsed.TotalMilliseconds
            };
        }
    }

    private async Task PerformBackgroundHealthCheckAsync(
        TimeSpan interval,
        IEnumerable<KeyValuePair<string, ProxyInfo>> proxyPool,
        CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, _timeProvider, token).ConfigureAwait(false);

                foreach (KeyValuePair<string, ProxyInfo> kvp in proxyPool)
                {
                    if (token.IsCancellationRequested)
                        break;

                    ProxyInfo proxy = kvp.Value;

                    if (_blacklistManager.IsBlacklisted(proxy))
                        continue;

                    await CheckProxyHealthAsync(proxy, token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    s_logHealthCheckCycleFailed(_logger, ex);
                }
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            _healthCheckCts.Cancel();
            _healthCheckCts.Dispose();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        _healthCheckClient?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// Result of a health check operation.
/// </summary>
public sealed class HealthCheckResult
{
    public bool Success { get; init; }
    public double LatencyMs { get; init; }
    public int? StatusCode { get; init; }
    public string? Error { get; init; }
}
