namespace Ghost.Resilience;

/// <summary>
/// Configuration options for a circuit breaker instance.
/// </summary>
public class CircuitBreakerOptions
{
    /// <summary>
    /// Gets or sets the number of consecutive failures before opening the circuit.
    /// </summary>
    public int FailureThreshold { get; set; } = 5;

    /// <summary>
    /// Gets or sets the duration the circuit remains open before transitioning to half-open.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum number of attempts allowed while half-open.
    /// </summary>
    public int HalfOpenMaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the time provider for time-based operations.
    /// Defaults to <see cref="TimeProvider.System"/>.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
