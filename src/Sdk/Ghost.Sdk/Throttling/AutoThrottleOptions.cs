namespace Ghost.Sdk.Throttling;

/// <summary>
/// Configuration options for the AutoThrottle adaptive rate limiting system.
/// </summary>
/// <remarks>
/// These options control how the throttle adapts to server response times,
/// with bounds to prevent both overwhelming the server and crawling too slowly.
/// </remarks>
public sealed class AutoThrottleOptions
{
    /// <summary>
    /// Gets or sets the initial delay in seconds before adaptive adjustments begin.
    /// </summary>
    /// <value>The starting delay in seconds. Default is 1.0.</value>
    /// <remarks>
    /// This is the delay used for the first few requests before the adaptive
    /// algorithm has enough data to make informed adjustments.
    /// </remarks>
    public double StartDelay { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the minimum allowed delay in seconds.
    /// </summary>
    /// <value>The minimum delay in seconds. Default is 0.1.</value>
    /// <remarks>
    /// Even when the server is responding very quickly, the delay will never
    /// fall below this value. This provides a safety floor to avoid hammering servers.
    /// </remarks>
    public double MinDelay { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets the maximum allowed delay in seconds.
    /// </summary>
    /// <value>The maximum delay in seconds. Default is 60.0.</value>
    /// <remarks>
    /// When the server is slow or overloaded, the delay will increase but never
    /// exceed this value. This prevents crawls from becoming unreasonably slow.
    /// </remarks>
    public double MaxDelay { get; set; } = 60.0;

    /// <summary>
    /// Gets or sets the target latency for server responses.
    /// </summary>
    /// <value>The target latency. Default is 1 second.</value>
    /// <remarks>
    /// The adaptive algorithm tries to maintain an average response time close to this value.
    /// If latency is significantly below target, delays decrease. If latency is above target,
    /// delays increase.
    /// </remarks>
    public TimeSpan TargetLatency { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the maximum number of latency samples to keep in the rolling window.
    /// </summary>
    /// <value>The maximum number of samples. Default is 100.</value>
    /// <remarks>
    /// The throttle maintains a sliding window of recent latency measurements.
    /// Larger values provide smoother adjustments but respond more slowly to changes.
    /// Smaller values adapt more quickly but may be more volatile.
    /// </remarks>
    public int MaxSamples { get; set; } = 100;
}
