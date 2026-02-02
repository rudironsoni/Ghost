namespace Ghost.Resilience;

/// <summary>
/// Defines a circuit breaker that protects operations from cascading failures.
/// </summary>
public interface ICircuitBreaker
{
    /// <summary>
    /// Executes the provided action with circuit breaker protection.
    /// </summary>
    /// <typeparam name="T">The type of the action result.</typeparam>
    /// <param name="action">The action to execute.</param>
    /// <returns>The action result.</returns>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action);

    /// <summary>
    /// Gets the current circuit state.
    /// </summary>
    CircuitState State { get; }

    /// <summary>
    /// Occurs when the circuit state changes.
    /// </summary>
    event EventHandler<CircuitStateChangedEventArgs> StateChanged;

    /// <summary>
    /// Gets a snapshot of circuit breaker metrics.
    /// </summary>
    /// <returns>The current metrics snapshot.</returns>
    CircuitBreakerMetrics GetMetrics();

    /// <summary>
    /// Gets the platform name associated with this circuit breaker.
    /// </summary>
    string Platform { get; }
}
