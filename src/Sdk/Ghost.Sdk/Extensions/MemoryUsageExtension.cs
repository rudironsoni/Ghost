using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Extensions;

/// <summary>
/// Monitors and limits memory usage during spider execution using GC-based tracking.
/// </summary>
/// <remarks>
/// This extension tracks managed heap memory via <see cref="System.GC.GetTotalMemory"/>,
/// providing configurable limits, warning thresholds, and optional garbage collection.
/// Designed to prevent out-of-memory errors in long-running crawls.
/// </remarks>
public sealed partial class MemoryUsageExtension : IMemoryUsageExtension
{
    private long _peakBytes;
    private readonly ILogger<MemoryUsageExtension> _logger;
    private readonly MemoryOptions _options;
    private bool _warningLogged;

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryUsageExtension"/> class.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <param name="options">Configuration options for memory monitoring.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="logger"/> or <paramref name="options"/> is null.
    /// </exception>
    public MemoryUsageExtension(ILogger<MemoryUsageExtension> logger, MemoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        _logger = logger;
        _options = options;
        MaxMemoryBytes = options.MaxMemoryBytes;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MemoryUsageExtension"/> class with default options.
    /// </summary>
    /// <param name="logger">The logger instance for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
    public MemoryUsageExtension(ILogger<MemoryUsageExtension> logger)
        : this(logger, new MemoryOptions())
    {
    }

    /// <inheritdoc/>
    public long MaxMemoryBytes { get; set; }

    /// <inheritdoc/>
    public Task<bool> CheckMemoryAsync(CancellationToken ct = default)
    {
        long current = GC.GetTotalMemory(forceFullCollection: false);

        // Update peak memory
        long currentPeak = Interlocked.Read(ref _peakBytes);
        if (current > currentPeak)
        {
            Interlocked.Exchange(ref _peakBytes, current);
        }

        // Check if limit exceeded
        if (MaxMemoryBytes > 0 && current > MaxMemoryBytes)
        {
            LogMemoryLimitExceeded(current, MaxMemoryBytes);
            return Task.FromResult(false);
        }

        // Check warning threshold
        if (MaxMemoryBytes > 0)
        {
            double usagePercent = (current / (double)MaxMemoryBytes) * 100;
            if (usagePercent >= _options.WarningThresholdPercent && !_warningLogged)
            {
                LogMemoryWarning(current, MaxMemoryBytes, usagePercent);
                _warningLogged = true;

                // Optionally trigger GC to try to reclaim memory
                if (_options.EnableGarbageCollection)
                {
                    LogTriggeringGarbageCollection();
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    // Re-check after GC
                    long afterGc = GC.GetTotalMemory(forceFullCollection: false);
                    LogGarbageCollectionCompleted(current, afterGc);
                }
            }
        }

        return Task.FromResult(true);
    }

    /// <inheritdoc/>
    public MemoryStats GetStats()
    {
        long current = GC.GetTotalMemory(forceFullCollection: false);
        return new MemoryStats
        {
            CurrentBytes = current,
            PeakBytes = Interlocked.Read(ref _peakBytes),
            MaxAllowedBytes = MaxMemoryBytes
        };
    }

    // LoggerMessage source generators
    [LoggerMessage(LogLevel.Warning, "Memory limit exceeded: {Current} bytes > {Max} bytes")]
    partial void LogMemoryLimitExceeded(long current, long max);

    [LoggerMessage(LogLevel.Warning, "Memory usage at {UsagePercent:F2}%: {Current} bytes / {Max} bytes")]
    partial void LogMemoryWarning(long current, long max, double usagePercent);

    [LoggerMessage(LogLevel.Information, "Triggering garbage collection to reclaim memory")]
    partial void LogTriggeringGarbageCollection();

    [LoggerMessage(LogLevel.Information, "Garbage collection completed: {Before} bytes -> {After} bytes")]
    partial void LogGarbageCollectionCompleted(long before, long after);
}
