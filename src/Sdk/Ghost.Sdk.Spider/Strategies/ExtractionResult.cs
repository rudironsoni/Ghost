namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Represents the result of an extraction strategy execution.
/// </summary>
public class ExtractionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the extraction was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the extracted data.
    /// </summary>
    public object? Data { get; init; }

    /// <summary>
    /// Gets or sets the error message if the extraction failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Gets or sets the exception that occurred during extraction.
    /// </summary>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets or sets the name of the strategy that produced this result.
    /// </summary>
    public string? StrategyName { get; init; }

    /// <summary>
    /// Gets or sets the duration of the extraction.
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets or sets the HTTP status code if applicable.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Gets or sets additional metadata about the extraction.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets or sets the strategy attempts that were made.
    /// </summary>
    public List<StrategyAttempt> Attempts { get; init; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether this result is from a fallback strategy.
    /// </summary>
    public bool IsFallback { get; init; }

    /// <summary>
    /// Creates a successful extraction result.
    /// </summary>
    /// <param name="data">The extracted data.</param>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="duration">The duration of the extraction.</param>
    /// <returns>A successful <see cref="ExtractionResult"/>.</returns>
    public static ExtractionResult CreateSuccess(object data, string strategyName, TimeSpan duration)
    {
        return new ExtractionResult
        {
            Success = true,
            Data = data,
            StrategyName = strategyName,
            Duration = duration
        };
    }

    /// <summary>
    /// Creates a failed extraction result.
    /// </summary>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="strategyName">The name of the strategy.</param>
    /// <param name="duration">The duration of the extraction.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <returns>A failed <see cref="ExtractionResult"/>.</returns>
    public static ExtractionResult CreateFailure(string errorMessage, string strategyName, TimeSpan duration, Exception? exception = null)
    {
        return new ExtractionResult
        {
            Success = false,
            ErrorMessage = errorMessage,
            StrategyName = strategyName,
            Duration = duration,
            Exception = exception
        };
    }
}
