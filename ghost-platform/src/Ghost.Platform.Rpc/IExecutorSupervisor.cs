namespace Ghost.Platform.Rpc;

/// <summary>
/// Supervisor interface for managing out-of-process executor lifecycle.
/// </summary>
public interface IExecutorSupervisor : IAsyncDisposable
{
    /// <summary>
    /// Starts the supervised executor process.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The executor client for communication.</returns>
    Task<IExecutorClient> StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the supervised executor process.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Restarts the supervised executor process.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The executor client for communication.</returns>
    Task<IExecutorClient> RestartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets whether the executor is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the process ID of the executor if running.
    /// </summary>
    int? ProcessId { get; }

    /// <summary>
    /// Gets the number of restart attempts.
    /// </summary>
    int RestartCount { get; }

    /// <summary>
    /// Gets the last restart time if any.
    /// </summary>
    DateTimeOffset? LastRestartTimeUtc { get; }

    /// <summary>
    /// Event raised when the executor process exits.
    /// </summary>
    event EventHandler<ExecutorExitedEventArgs>? ExecutorExited;

    /// <summary>
    /// Event raised when the executor is restarted.
    /// </summary>
    event EventHandler<ExecutorRestartedEventArgs>? ExecutorRestarted;
}

/// <summary>
/// Event arguments for executor exited event.
/// </summary>
public sealed class ExecutorExitedEventArgs : EventArgs
{
    /// <summary>
    /// Exit code of the process.
    /// </summary>
    public required int ExitCode { get; init; }

    /// <summary>
    /// Whether the exit was unexpected.
    /// </summary>
    public required bool WasUnexpected { get; init; }

    /// <summary>
    /// Reason for exit if known.
    /// </summary>
    public string? Reason { get; init; }
}

/// <summary>
/// Event arguments for executor restarted event.
/// </summary>
public sealed class ExecutorRestartedEventArgs : EventArgs
{
    /// <summary>
    /// Previous process ID.
    /// </summary>
    public required int PreviousProcessId { get; init; }

    /// <summary>
    /// New process ID.
    /// </summary>
    public required int NewProcessId { get; init; }

    /// <summary>
    /// Restart attempt number.
    /// </summary>
    public required int AttemptNumber { get; init; }
}
