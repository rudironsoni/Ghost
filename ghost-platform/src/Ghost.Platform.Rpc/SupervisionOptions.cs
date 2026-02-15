namespace Ghost.Platform.Rpc;

/// <summary>
/// Options for executor supervision.
/// </summary>
public sealed class SupervisionOptions
{
    /// <summary>
    /// Path to the executor executable.
    /// </summary>
    public required string ExecutorPath { get; init; }

    /// <summary>
    /// Arguments to pass to the executor.
    /// </summary>
    public string ExecutorArguments { get; init; } = string.Empty;

    /// <summary>
    /// Working directory for the executor process.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Maximum number of restart attempts.
    /// </summary>
    public int MaxRestartAttempts { get; init; } = 3;

    /// <summary>
    /// Delay between restart attempts.
    /// </summary>
    public TimeSpan RestartDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Timeout for executor startup.
    /// </summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Timeout for graceful shutdown.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Timeout for handshake.
    /// </summary>
    public TimeSpan HandshakeTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Whether to restart on crash.
    /// </summary>
    public bool RestartOnCrash { get; init; } = true;

    /// <summary>
    /// Whether to restart on unhandled exception.
    /// </summary>
    public bool RestartOnException { get; init; } = true;

    /// <summary>
    /// Whether to kill process on timeout during shutdown.
    /// </summary>
    public bool KillOnShutdownTimeout { get; init; } = true;

    /// <summary>
    /// Environment variables to set for the executor process.
    /// </summary>
    public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; init; } =
        new Dictionary<string, string?>();

    /// <summary>
    /// Whether to redirect standard output.
    /// </summary>
    public bool RedirectStandardOutput { get; init; } = true;

    /// <summary>
    /// Whether to redirect standard error.
    /// </summary>
    public bool RedirectStandardError { get; init; } = true;

    /// <summary>
    /// Whether to redirect standard input.
    /// </summary>
    public bool RedirectStandardInput { get; init; } = true;

    /// <summary>
    /// Whether to create a new window for the process.
    /// </summary>
    public bool CreateNoWindow { get; init; } = true;

    /// <summary>
    /// Whether to use shell execute.
    /// </summary>
    public bool UseShellExecute { get; init; }
}
