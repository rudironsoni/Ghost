using Ghost.Queue;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ghost.WebApi.Metrics;

/// <summary>
/// Background service that polls Redis queue depth and exposes metrics for HPA.
/// </summary>
public sealed class RedisQueueMetricsService : BackgroundService
{
    private readonly IJobDispatcher _dispatcher;
    private readonly ILogger<RedisQueueMetricsService> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(15);
    private static readonly Action<ILogger, int, int, int, int, Exception?> s_metricsUpdated =
        LoggerMessage.Define<int, int, int, int>(
            LogLevel.Debug,
            new EventId(1, "MetricsUpdated"),
            "Redis queue metrics updated: pending={PendingCount}, active={ActiveCount}, completed={CompletedCount}, dead={DeadCount}");

    private static readonly Action<ILogger, Exception?> s_metricsPollError =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(2, "MetricsPollError"),
            "Error polling Redis queue metrics");

    private static readonly Action<ILogger, double, Exception?> s_serviceStarting =
        LoggerMessage.Define<double>(
            LogLevel.Information,
            new EventId(3, "ServiceStarting"),
            "Redis queue metrics service starting, polling every {Interval} seconds");

    private static readonly Action<ILogger, Exception?> s_serviceStopped =
        LoggerMessage.Define(
            LogLevel.Information,
            new EventId(4, "ServiceStopped"),
            "Redis queue metrics service stopped");

    public RedisQueueMetricsService(
        IJobDispatcher dispatcher,
        ILogger<RedisQueueMetricsService> logger)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(logger);

        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current pending queue count.
    /// </summary>
    public int PendingCount { get; private set; }

    /// <summary>
    /// Gets the current active jobs count.
    /// </summary>
    public int ActiveCount { get; private set; }

    /// <summary>
    /// Gets the current completed jobs count.
    /// </summary>
    public int CompletedCount { get; private set; }

    /// <summary>
    /// Gets the current dead letter queue count.
    /// </summary>
    public int DeadCount { get; private set; }

    /// <summary>
    /// Gets the timestamp of the last metrics update.
    /// </summary>
    public DateTime LastUpdate { get; private set; } = DateTime.UtcNow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        s_serviceStarting(_logger, _pollInterval.TotalSeconds, null);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await UpdateMetricsAsync(stoppingToken);
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                s_metricsPollError(_logger, ex);
                await Task.Delay(_pollInterval, stoppingToken);
            }
        }

        s_serviceStopped(_logger, null);
    }

    private async Task UpdateMetricsAsync(CancellationToken cancellationToken)
    {
        PendingCount = await _dispatcher.GetPendingCountAsync(cancellationToken);
        ActiveCount = await _dispatcher.GetActiveCountAsync(cancellationToken);
        CompletedCount = await _dispatcher.GetCompletedCountAsync(cancellationToken);
        DeadCount = await _dispatcher.GetDeadCountAsync(cancellationToken);
        LastUpdate = DateTime.UtcNow;

        s_metricsUpdated(_logger, PendingCount, ActiveCount, CompletedCount, DeadCount, null);
    }
}
