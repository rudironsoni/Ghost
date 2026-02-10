namespace Ghost.Sdk.Middleware;

/// <summary>
/// Configuration options for proxy management and rotation.
/// </summary>
/// <remarks>
/// These options control proxy health checking, failure thresholds, and recovery behavior.
/// Proxies exceeding the failure threshold are temporarily excluded from rotation until
/// the retry period elapses.
/// </remarks>
public class ProxyOptions
{
    /// <summary>
    /// Gets or sets the maximum number of consecutive failures before a proxy is excluded.
    /// </summary>
    /// <value>
    /// The maximum failure count. Default is 3.
    /// </value>
    /// <remarks>
    /// When a proxy's failure count reaches this threshold, it will be excluded from
    /// rotation until the RetryAfter period has elapsed or a successful request resets the counter.
    /// </remarks>
    public int MaxFailures { get; set; } = 3;

    /// <summary>
    /// Gets or sets the duration to wait before retrying a failed proxy.
    /// </summary>
    /// <value>
    /// The retry delay. Default is 5 minutes.
    /// </value>
    /// <remarks>
    /// After this period elapses, a failed proxy's failure counter is reset and it becomes
    /// eligible for rotation again. This prevents permanently excluding proxies that may
    /// have recovered from transient failures.
    /// </remarks>
    public TimeSpan RetryAfter { get; set; } = TimeSpan.FromMinutes(5);
}
