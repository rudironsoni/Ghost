namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Represents a single attempt to execute a strategy.
/// </summary>
public class StrategyAttempt
{
    /// <summary>
    /// Gets or sets the name of the strategy that was attempted.
    /// </summary>
    public required string StrategyName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the attempt was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the error message if the attempt failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets the exception that occurred during the attempt.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets or sets the duration of the attempt.
    /// </summary>
    public required TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets or sets the timestamp when the attempt started.
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the attempt ended.
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Gets or sets the HTTP status code if applicable.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Gets or sets the reason why this strategy was attempted.
    /// </summary>
    public string? Reason { get; init; }

    /// <summary>
    /// Gets or sets the conditions that triggered this attempt.
    /// </summary>
    public List<string> TriggerConditions { get; init; } = [];

    /// <summary>
    /// Gets or sets additional metadata about the attempt.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether this attempt was a retry.
    /// </summary>
    public bool IsRetry { get; init; }

    /// <summary>
    /// Gets or sets the retry attempt number if this is a retry.
    /// </summary>
    public int? RetryNumber { get; init; }

    /// <summary>
    /// Creates a successful strategy attempt.
    /// </summary>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="duration">The duration of the attempt.</param>
    /// <param name="startTime">The start time of the attempt.</param>
    /// <returns>A successful <see cref="StrategyAttempt"/>.</returns>
    public static StrategyAttempt CreateSuccess(string strategyName, TimeSpan duration, DateTime startTime)
    {
        return new StrategyAttempt
        {
            StrategyName = strategyName,
            Success = true,
            Duration = duration,
            StartTime = startTime,
            EndTime = startTime + duration
        };
    }

    /// <summary>
    /// Creates a failed strategy attempt.
    /// </summary>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="duration">The duration of the attempt.</param>
    /// <param name="startTime">The start time of the attempt.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A failed <see cref="StrategyAttempt"/>.</returns>
    public static StrategyAttempt CreateFailure(string strategyName, string errorMessage, TimeSpan duration, DateTime startTime, Exception? exception = null)
    {
        return new StrategyAttempt
        {
            StrategyName = strategyName,
            Success = false,
            ErrorMessage = errorMessage,
            Duration = duration,
            StartTime = startTime,
            EndTime = startTime + duration,
            Exception = exception
        };
    }
}
