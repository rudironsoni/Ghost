namespace Ghost.Resilience;

/// <summary>
/// Represents the current state of a circuit breaker.
/// </summary>
public enum CircuitState
{
    /// <summary>
    /// Circuit is closed and requests are allowed.
    /// </summary>
    Closed,

    /// <summary>
    /// Circuit is open and requests are rejected.
    /// </summary>
    Open,

    /// <summary>
    /// Circuit is half-open and allows limited test requests.
    /// </summary>
    HalfOpen
}
