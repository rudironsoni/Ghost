using System.Collections.Concurrent;

namespace Ghost.Sdk.Throttling;

/// <summary>
/// Adaptive rate limiting implementation that adjusts download delays based on server response times.
/// </summary>
/// <remarks>
/// This implementation uses a simple feedback control algorithm: when server latency is low,
/// delays are reduced to increase throughput. When latency is high, delays are increased to
/// reduce server load. The adjustment is bounded by configured minimum and maximum values.
/// Thread-safe for use in concurrent scraping scenarios.
/// </remarks>
public sealed class AutoThrottle : IAutoThrottle
{
    private readonly AutoThrottleOptions _options;
    private readonly ConcurrentQueue<TimeSpan> _latencies = new();
    private double _currentDelay;
    private readonly object _delayLock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AutoThrottle"/> class.
    /// </summary>
    /// <param name="options">Configuration options for the throttle.</param>
    /// <exception cref="ArgumentNullException">Thrown when options is null.</exception>
    /// <exception cref="ArgumentException">Thrown when options contain invalid values.</exception>
    public AutoThrottle(AutoThrottleOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MinDelay < 0)
        {
            throw new ArgumentException("MinDelay must be non-negative", nameof(options));
        }

        if (options.MaxDelay < options.MinDelay)
        {
            throw new ArgumentException("MaxDelay must be greater than or equal to MinDelay", nameof(options));
        }

        if (options.StartDelay < options.MinDelay || options.StartDelay > options.MaxDelay)
        {
            throw new ArgumentException("StartDelay must be between MinDelay and MaxDelay", nameof(options));
        }

        if (options.TargetLatency <= TimeSpan.Zero)
        {
            throw new ArgumentException("TargetLatency must be positive", nameof(options));
        }

        if (options.MaxSamples <= 0)
        {
            throw new ArgumentException("MaxSamples must be positive", nameof(options));
        }

        _options = options;
        _currentDelay = options.StartDelay;
    }

    /// <summary>
    /// Gets the current adaptive delay that should be applied between requests.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The delay in seconds that should be waited before the next request.
    /// </returns>
    /// <remarks>
    /// This method is thread-safe and will return the most recently calculated delay
    /// based on observed server latencies.
    /// </remarks>
    public Task<double> GetDelayAsync(CancellationToken ct = default)
    {
        lock (_delayLock)
        {
            return Task.FromResult(_currentDelay);
        }
    }

    /// <summary>
    /// Records a server response latency measurement for adaptive adjustment.
    /// </summary>
    /// <param name="latency">The measured response latency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method enqueues the latency measurement, maintains the rolling window size,
    /// calculates the average latency, and adjusts the current delay accordingly.
    /// The algorithm uses multiplicative increase/decrease with factors of 1.1 and 0.9
    /// to provide stable, gradual adjustments.
    /// </remarks>
    public Task RecordLatencyAsync(TimeSpan latency, CancellationToken ct = default)
    {
        if (latency < TimeSpan.Zero)
        {
            throw new ArgumentException("Latency must be non-negative", nameof(latency));
        }

        // Add new latency sample
        _latencies.Enqueue(latency);

        // Maintain sliding window by removing old samples
        while (_latencies.Count > _options.MaxSamples)
        {
            _latencies.TryDequeue(out _);
        }

        // Calculate average latency from all samples
        if (!_latencies.IsEmpty)
        {
            double avgLatency = _latencies.Average(l => l.TotalMilliseconds);
            double targetLatencyMs = _options.TargetLatency.TotalMilliseconds;

            lock (_delayLock)
            {
                // Apply hysteresis: only adjust if significantly above/below target
                // This prevents oscillation and provides stability
                if (avgLatency < targetLatencyMs * 0.8)
                {
                    // Server is responding quickly, decrease delay (speed up)
                    _currentDelay = Math.Max(_options.MinDelay, _currentDelay * 0.9);
                }
                else if (avgLatency > targetLatencyMs * 1.2)
                {
                    // Server is responding slowly, increase delay (slow down)
                    _currentDelay = Math.Min(_options.MaxDelay, _currentDelay * 1.1);
                }
                // If latency is within 80%-120% of target, maintain current delay
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the current number of latency samples in the rolling window.
    /// </summary>
    /// <value>The number of samples currently stored.</value>
    /// <remarks>
    /// This property is useful for monitoring the throttle's state and understanding
    /// when it has enough data to make informed adjustments.
    /// </remarks>
    public int SampleCount => _latencies.Count;

    /// <summary>
    /// Gets the average latency from all samples in the current window.
    /// </summary>
    /// <value>
    /// The average latency, or <see cref="TimeSpan.Zero"/> if no samples exist.
    /// </value>
    /// <remarks>
    /// This property provides visibility into the server's response characteristics
    /// and can be used for monitoring and debugging.
    /// </remarks>
    public TimeSpan AverageLatency
    {
        get
        {
            if (_latencies.IsEmpty)
            {
                return TimeSpan.Zero;
            }

            double avgMs = _latencies.Average(l => l.TotalMilliseconds);
            return TimeSpan.FromMilliseconds(avgMs);
        }
    }
}
