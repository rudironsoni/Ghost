namespace Ghost.Resilience;

/// <summary>
/// Metrics collected for a circuit breaker instance.
/// </summary>
public class CircuitBreakerMetrics
{
    /// <summary>
    /// Gets or sets the number of failures recorded.
    /// </summary>
    public int FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the number of successes recorded.
    /// </summary>
    public int SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last failure.
    /// </summary>
    public DateTime LastFailure { get; set; }

    /// <summary>
    /// Gets or sets the time spent in the current state.
    /// </summary>
    public TimeSpan TimeInCurrentState { get; set; }
}
