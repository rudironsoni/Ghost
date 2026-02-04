namespace Ghost.Sdk.Spider.Engine;

/// <summary>
/// Represents the result of a spider execution.
/// </summary>
public class SpiderResult
{
    /// <summary>
    /// Gets or sets the spider name.
    /// </summary>
    /// <value>The name of the spider that was executed.</value>
    public required string SpiderName { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether the spider completed successfully.
    /// </summary>
    /// <value><c>true</c> if successful; otherwise, <c>false</c>.</value>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the total number of requests processed.
    /// </summary>
    /// <value>The request count.</value>
    public int RequestsProcessed { get; init; }

    /// <summary>
    /// Gets or sets the number of successful requests.
    /// </summary>
    /// <value>The successful request count.</value>
    public int RequestsSucceeded { get; init; }

    /// <summary>
    /// Gets or sets the number of failed requests.
    /// </summary>
    /// <value>The failed request count.</value>
    public int RequestsFailed { get; init; }

    /// <summary>
    /// Gets or sets the total number of items extracted.
    /// </summary>
    /// <value>The extracted item count.</value>
    public int ItemsExtracted { get; init; }

    /// <summary>
    /// Gets or sets the execution start time.
    /// </summary>
    /// <value>The UTC start timestamp.</value>
    public DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Gets or sets the execution end time.
    /// </summary>
    /// <value>The UTC end timestamp.</value>
    public DateTimeOffset CompletedAt { get; init; }

    /// <summary>
    /// Gets the total execution duration.
    /// </summary>
    /// <value>The time elapsed during execution.</value>
    public TimeSpan Duration => CompletedAt - StartedAt;

    /// <summary>
    /// Gets or sets error information if the spider failed.
    /// </summary>
    /// <value>The error message, or null if successful.</value>
    public string? Error { get; init; }

    /// <summary>
    /// Gets or sets the exception that occurred, if any.
    /// </summary>
    /// <value>The exception, or null if successful.</value>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets or sets additional statistics and metadata.
    /// </summary>
    /// <value>Dictionary of custom statistics.</value>
    public Dictionary<string, object> Statistics { get; init; } = new();

    /// <summary>
    /// Creates a successful spider result.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <param name="requestsProcessed">Number of requests processed.</param>
    /// <param name="itemsExtracted">Number of items extracted.</param>
    /// <param name="startedAt">Start timestamp.</param>
    /// <param name="completedAt">Completion timestamp.</param>
    /// <returns>A successful <see cref="SpiderResult"/>.</returns>
    public static SpiderResult CreateSuccess(
        string spiderName,
        int requestsProcessed,
        int itemsExtracted,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        return new SpiderResult
        {
            SpiderName = spiderName,
            Success = true,
            RequestsProcessed = requestsProcessed,
            RequestsSucceeded = requestsProcessed,
            RequestsFailed = 0,
            ItemsExtracted = itemsExtracted,
            StartedAt = startedAt,
            CompletedAt = completedAt
        };
    }

    /// <summary>
    /// Creates a failed spider result.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <param name="error">The error message.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="startedAt">Start timestamp.</param>
    /// <returns>A failed <see cref="SpiderResult"/>.</returns>
    public static SpiderResult CreateFailure(
        string spiderName,
        string error,
        Exception? exception,
        DateTimeOffset startedAt)
    {
        return new SpiderResult
        {
            SpiderName = spiderName,
            Success = false,
            RequestsProcessed = 0,
            RequestsSucceeded = 0,
            RequestsFailed = 0,
            ItemsExtracted = 0,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow,
            Error = error,
            Exception = exception
        };
    }
}
