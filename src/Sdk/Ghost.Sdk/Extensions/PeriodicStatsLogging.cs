using Ghost.Sdk.Statistics;
using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Logs spider statistics periodically during execution using a timer-based approach.
/// </summary>
/// <remarks>
/// This extension uses <see cref="System.Threading.Timer"/> to periodically query
/// the stats collector and log current metrics. Designed for long-running spiders
/// where periodic status updates are valuable for monitoring and debugging.
/// </remarks>
public sealed partial class PeriodicStatsLogging : IPeriodicStatsLogging, IDisposable
{
    private readonly IStatsCollector _statsCollector;
    private readonly ILogger<PeriodicStatsLogging> _logger;
    private Timer? _timer;
    private string? _currentSpiderId;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="PeriodicStatsLogging"/> class.
    /// </summary>
    /// <param name="statsCollector">The stats collector to query for metrics.</param>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="statsCollector"/> or <paramref name="logger"/> is null.
    /// </exception>
    public PeriodicStatsLogging(IStatsCollector statsCollector, ILogger<PeriodicStatsLogging> logger)
    {
        ArgumentNullException.ThrowIfNull(statsCollector);
        ArgumentNullException.ThrowIfNull(logger);

        _statsCollector = statsCollector;
        _logger = logger;
    }

    /// <inheritdoc/>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public void StartLogging(string spiderId)
    {
        ArgumentNullException.ThrowIfNull(spiderId);

        ObjectDisposedException.ThrowIf(_disposed, this);

        _currentSpiderId = spiderId;

        // Dispose existing timer if any
        _timer?.Dispose();

        // Create new timer with the current interval
        _timer = new Timer(LogStatsCallback, null, Interval, Interval);

        LogStartedPeriodicLogging(spiderId, Interval.TotalSeconds);
    }

    /// <inheritdoc/>
    public void StopLogging()
    {
        if (_timer != null)
        {
            _timer.Change(Timeout.Infinite, Timeout.Infinite);
            _timer.Dispose();
            _timer = null;

            if (_currentSpiderId != null)
            {
                LogStoppedPeriodicLogging(_currentSpiderId);
            }
        }

        _currentSpiderId = null;
    }

    /// <summary>
    /// Releases all resources used by the <see cref="PeriodicStatsLogging"/>.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopLogging();
        _disposed = true;
    }

    private void LogStatsCallback(object? state)
    {
        if (_currentSpiderId == null)
        {
            return;
        }

        try
        {
            var stats = _statsCollector.GetStats(_currentSpiderId);

            LogSpiderStats(
                stats.SpiderId,
                stats.RequestCount,
                stats.ResponseCount,
                stats.ErrorCount,
                stats.ItemCount,
                stats.RequestsPerSecond
            );
        }
        catch (Exception ex)
        {
            LogStatsCollectionFailed(_currentSpiderId, ex);
        }
    }

    // LoggerMessage source generators
    [LoggerMessage(LogLevel.Information, "Started periodic stats logging for spider {SpiderId} with interval {IntervalSeconds}s")]
    partial void LogStartedPeriodicLogging(string spiderId, double intervalSeconds);

    [LoggerMessage(LogLevel.Information, "Stopped periodic stats logging for spider {SpiderId}")]
    partial void LogStoppedPeriodicLogging(string spiderId);

    [LoggerMessage(
        LogLevel.Information,
        "Spider {SpiderId}: {Requests} requests, {Responses} responses, {Errors} errors, {Items} items, {Rps:F2} req/s"
    )]
    partial void LogSpiderStats(
        string spiderId,
        long requests,
        long responses,
        long errors,
        long items,
        double rps
    );

    [LoggerMessage(LogLevel.Warning, "Failed to collect stats for spider {SpiderId}")]
    partial void LogStatsCollectionFailed(string spiderId, Exception ex);
}
