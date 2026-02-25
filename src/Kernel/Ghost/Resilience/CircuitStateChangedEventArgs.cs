namespace Ghost.Resilience;

/// <summary>
/// Event arguments for circuit state changes.
/// </summary>
public sealed class CircuitStateChangedEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CircuitStateChangedEventArgs"/> class.
    /// </summary>
    /// <param name="previousState">The previous circuit state.</param>
    /// <param name="currentState">The new circuit state.</param>
    /// <param name="platform">The platform name associated with the circuit.</param>
    /// <param name="changedAt">The time the state change occurred.</param>
    public CircuitStateChangedEventArgs(CircuitState previousState, CircuitState currentState, string platform, DateTime changedAt)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Platform = platform;
        ChangedAt = changedAt;
    }

    /// <summary>
    /// Gets the previous circuit state.
    /// </summary>
    public CircuitState PreviousState { get; }

    /// <summary>
    /// Gets the current circuit state.
    /// </summary>
    public CircuitState CurrentState { get; }

    /// <summary>
    /// Gets the platform name associated with the circuit.
    /// </summary>
    public string Platform { get; }

    /// <summary>
    /// Gets the time the state change occurred.
    /// </summary>
    public DateTime ChangedAt { get; }
}
