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

    private int _pendingCount;
    private int _activeCount;
    private int _completedCount;
    private int _deadCount;
    private DateTime _lastUpdate = DateTime.UtcNow;

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
    public int PendingCount => _pendingCount;

    /// <summary>
    /// Gets the current active jobs count.
    /// </summary>
    public int ActiveCount => _activeCount;

    /// <summary>
    /// Gets the current completed jobs count.
    /// </summary>
    public int CompletedCount => _completedCount;

    /// <summary>
    /// Gets the current dead letter queue count.
    /// </summary>
    public int DeadCount => _deadCount;

    /// <summary>
    /// Gets the timestamp of the last metrics update.
    /// </summary>
    public DateTime LastUpdate => _lastUpdate;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Redis queue metrics service starting, polling every {Interval} seconds", _pollInterval.TotalSeconds);

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

        _logger.LogInformation("Redis queue metrics service stopped");
    }

    private async Task UpdateMetricsAsync(CancellationToken cancellationToken)
    {
        _pendingCount = await _dispatcher.GetPendingCountAsync(cancellationToken);
        _activeCount = await _dispatcher.GetActiveCountAsync(cancellationToken);
        _completedCount = await _dispatcher.GetCompletedCountAsync(cancellationToken);
        _deadCount = await _dispatcher.GetDeadCountAsync(cancellationToken);
        _lastUpdate = DateTime.UtcNow;

        s_metricsUpdated(_logger, _pendingCount, _activeCount, _completedCount, _deadCount, null);
    }
}
