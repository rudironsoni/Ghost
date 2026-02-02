using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Ghost.Scraper.DotnetSpider.Monitoring;

/// <summary>
/// Provides monitoring and health tracking for job scraper platforms.
/// </summary>
public class JobScraperMonitoringService
{
    private readonly ConcurrentDictionary<string, PlatformMetrics> _metricsByPlatform = new();
    private readonly ILogger<JobScraperMonitoringService> _logger;
    private readonly TimeProvider _timeProvider;

    // Logger message definitions
    private static readonly Action<ILogger, string, bool, long, Exception?> LogRequest =
        LoggerMessage.Define<string, bool, long>(
            LogLevel.Debug,
            new EventId(1001, "RequestRecorded"),
            "Request recorded for platform '{PlatformName}'. Success: {Success}, Latency: {LatencyMs}ms");

    private static readonly Action<ILogger, string, double, Exception?> LogHealthStatus =
        LoggerMessage.Define<string, double>(
            LogLevel.Information,
            new EventId(1002, "HealthStatusCalculated"),
            "Health status calculated for platform '{PlatformName}'. Success rate: {SuccessRate}%");

    private static readonly Action<ILogger, string, string, Exception?> LogErrorRecorded =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(1003, "ErrorRecorded"),
            "Error recorded for platform '{PlatformName}'. Category: {ErrorCategory}");

    private static readonly Action<ILogger, string, Exception?> LogAlertThreshold =
        LoggerMessage.Define<string>(
            LogLevel.Critical,
            new EventId(1004, "AlertThresholdCrossed"),
            "Alert threshold crossed for platform '{PlatformName}'");

    private static readonly Action<ILogger, string, Exception?> LogPlatformMetricsRetrieved =
        LoggerMessage.Define<string>(
            LogLevel.Debug,
            new EventId(1005, "PlatformMetricsRetrieved"),
            "Platform metrics retrieved for '{PlatformName}'");

    private static readonly Action<ILogger, int, Exception?> LogAllMetricsRetrieved =
        LoggerMessage.Define<int>(
            LogLevel.Debug,
            new EventId(1006, "AllMetricsRetrieved"),
            "All metrics retrieved. Total platforms: {PlatformCount}");

    /// <summary>
    /// Initializes a new instance of the <see cref="JobScraperMonitoringService"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="timeProvider">Optional time provider for testing. Defaults to system time.</param>
    public JobScraperMonitoringService(
        ILogger<JobScraperMonitoringService> logger,
        TimeProvider? timeProvider = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Records a request outcome for a specific platform.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <param name="success">Whether the request was successful.</param>
    /// <param name="latencyMs">The request latency in milliseconds.</param>
    /// <param name="errorCategory">Optional error category if the request failed.</param>
    public void RecordRequest(string platformName, bool success, long latencyMs, string? errorCategory = null)
    {
        if (string.IsNullOrWhiteSpace(platformName))
            throw new ArgumentException("Platform name cannot be null or whitespace.", nameof(platformName));

        var metrics = _metricsByPlatform.GetOrAdd(platformName, _ => new PlatformMetrics());

        metrics.TotalRequests++;
        if (success)
        {
            metrics.SuccessfulRequests++;
        }
        else
        {
            metrics.FailedRequests++;
            if (!string.IsNullOrEmpty(errorCategory))
            {
                metrics.ErrorCategories.TryGetValue(errorCategory, out var count);
                metrics.ErrorCategories[errorCategory] = count + 1;
                LogErrorRecorded(_logger, platformName, errorCategory, null);
            }
        }

        // Update average latency
        metrics.AverageLatencyMs = (metrics.AverageLatencyMs * (metrics.TotalRequests - 1) + latencyMs) / metrics.TotalRequests;

        LogRequest(_logger, platformName, success, latencyMs, null);
    }

    /// <summary>
    /// Gets the current health status of a specific platform.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <returns>A PlatformHealth object containing current health metrics.</returns>
    public PlatformHealth GetPlatformHealth(string platformName)
    {
        if (string.IsNullOrWhiteSpace(platformName))
            throw new ArgumentException("Platform name cannot be null or whitespace.", nameof(platformName));

        if (!_metricsByPlatform.TryGetValue(platformName, out var metrics))
        {
            return new PlatformHealth
            {
                PlatformName = platformName,
                Status = HealthStatus.Healthy,
                SuccessRate = 100,
                ErrorCount = 0,
                LastChecked = _timeProvider.GetUtcNow()
            };
        }

        var successRate = CalculateSuccessRate(platformName);
        var status = DetermineHealthStatus(successRate);

        LogHealthStatus(_logger, platformName, successRate, null);
        LogPlatformMetricsRetrieved(_logger, platformName, null);

        return new PlatformHealth
        {
            PlatformName = platformName,
            Status = status,
            SuccessRate = successRate,
            ErrorCount = metrics.FailedRequests,
            LastChecked = _timeProvider.GetUtcNow()
        };
    }

    /// <summary>
    /// Gets the health status for all monitored platforms.
    /// </summary>
    /// <returns>A list of PlatformHealth objects for all platforms.</returns>
    public List<PlatformHealth> GetAllPlatformHealth()
    {
        var healthList = _metricsByPlatform.Keys
            .Select(GetPlatformHealth)
            .ToList();

        LogAllMetricsRetrieved(_logger, healthList.Count, null);
        return healthList;
    }

    /// <summary>
    /// Gets a snapshot of current metrics across all platforms.
    /// </summary>
    /// <returns>A JobScraperMetrics object containing aggregated metrics.</returns>
    public JobScraperMetrics GetCurrentMetrics()
    {
        var perPlatformMetrics = new Dictionary<string, PlatformMetrics>();

        foreach (var kvp in _metricsByPlatform)
        {
            var metrics = new PlatformMetrics
            {
                TotalRequests = kvp.Value.TotalRequests,
                SuccessfulRequests = kvp.Value.SuccessfulRequests,
                FailedRequests = kvp.Value.FailedRequests,
                AverageLatencyMs = kvp.Value.AverageLatencyMs,
                ErrorCategories = new Dictionary<string, int>(kvp.Value.ErrorCategories)
            };
            perPlatformMetrics[kvp.Key] = metrics;
        }

        return new JobScraperMetrics
        {
            PerPlatformMetrics = perPlatformMetrics,
            Timestamp = _timeProvider.GetUtcNow()
        };
    }

    /// <summary>
    /// Gets the current health status for a specific platform.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <returns>The HealthStatus enum value.</returns>
    public HealthStatus CheckHealthStatus(string platformName)
    {
        return GetPlatformHealth(platformName).Status;
    }

    /// <summary>
    /// Determines whether an alert should be triggered for a platform.
    /// Alerts are triggered when success rate falls below 70%.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <returns>True if alert threshold is crossed; otherwise, false.</returns>
    public bool ShouldAlert(string platformName)
    {
        var successRate = CalculateSuccessRate(platformName);
        var shouldAlert = successRate < 70;

        if (shouldAlert)
        {
            LogAlertThreshold(_logger, platformName, null);
        }

        return shouldAlert;
    }

    /// <summary>
    /// Calculates the success rate for a specific platform.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <returns>Success rate as a percentage (0-100).</returns>
    private double CalculateSuccessRate(string platformName)
    {
        if (!_metricsByPlatform.TryGetValue(platformName, out var metrics) || metrics.TotalRequests == 0)
        {
            return 100;
        }

        return (double)metrics.SuccessfulRequests / metrics.TotalRequests * 100;
    }

    /// <summary>
    /// Determines the health status based on success rate.
    /// </summary>
    /// <param name="successRate">The success rate as a percentage (0-100).</param>
    /// <returns>The appropriate HealthStatus.</returns>
    private static HealthStatus DetermineHealthStatus(double successRate)
    {
        return successRate switch
        {
            >= 90 => HealthStatus.Healthy,
            >= 70 => HealthStatus.Degraded,
            _ => HealthStatus.Unhealthy
        };
    }

    /// <summary>
    /// Gets error categories aggregated by count for a specific platform.
    /// </summary>
    /// <param name="platformName">The name of the platform.</param>
    /// <returns>Dictionary of error categories and their counts.</returns>
    private Dictionary<string, int> GetErrorCategories(string platformName)
    {
        if (_metricsByPlatform.TryGetValue(platformName, out var metrics))
        {
            return new Dictionary<string, int>(metrics.ErrorCategories);
        }

        return new Dictionary<string, int>();
    }
}
