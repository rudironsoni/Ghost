using Microsoft.Extensions.Logging;

namespace Ghost.Logging;

/// <summary>
/// Structured log events for job scraping operations.
/// </summary>
public static partial class ScrapeEvents
{
    /// <summary>
    /// Logs a successful job search completion.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="platform">Platform name.</param>
    /// <param name="query">Search query.</param>
    /// <param name="resultCount">Number of results returned.</param>
    /// <param name="durationMs">Search duration in milliseconds.</param>
    /// <param name="strategy">Search strategy used.</param>
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Job search completed. Platform={Platform} Query={Query} Results={ResultCount} Duration={DurationMs}ms Strategy={Strategy}")]
    public static partial void LogSearchCompleted(
        this ILogger logger,
        string platform,
        string query,
        int resultCount,
        long durationMs,
        string strategy);

    /// <summary>
    /// Logs a failed job search.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="platform">Platform name.</param>
    /// <param name="query">Search query.</param>
    /// <param name="circuitState">Circuit breaker state.</param>
    /// <param name="ex">Exception encountered.</param>
    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Job search failed. Platform={Platform} Query={Query} Circuit={CircuitState}")]
    public static partial void LogSearchFailed(
        this ILogger logger,
        string platform,
        string query,
        string circuitState,
        Exception ex);

    /// <summary>
    /// Logs a circuit breaker state change.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="platform">Platform name.</param>
    /// <param name="oldState">Previous state.</param>
    /// <param name="newState">Current state.</param>
    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Warning,
        Message = "Circuit breaker state changed. Platform={Platform} From={OldState} To={NewState}")]
    public static partial void LogCircuitStateChanged(
        this ILogger logger,
        string platform,
        string oldState,
        string newState);

    /// <summary>
    /// Logs when a rate limit is encountered.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="platform">Platform name.</param>
    /// <param name="retryAfterSeconds">Retry-after time in seconds.</param>
    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Warning,
        Message = "Rate limit hit. Platform={Platform} RetryAfter={RetryAfterSeconds}s")]
    public static partial void LogRateLimitHit(
        this ILogger logger,
        string platform,
        int retryAfterSeconds);

    /// <summary>
    /// Logs when a job is added to the dead letter queue.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="platform">Platform name.</param>
    /// <param name="jobId">Job identifier.</param>
    /// <param name="error">Error message.</param>
    [LoggerMessage(
        EventId = 1005,
        Level = LogLevel.Information,
        Message = "Job added to DLQ. Platform={Platform} JobId={JobId} Error={Error}")]
    public static partial void LogJobAddedToDlq(
        this ILogger logger,
        string platform,
        string jobId,
        string error);
}
