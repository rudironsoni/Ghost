namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Represents performance and execution metrics for a strategy.
/// </summary>
public class StrategyMetrics
{
    /// <summary>
    /// Gets or sets the name of the strategy.
    /// </summary>
    public required string StrategyName { get; init; }

    /// <summary>
    /// Gets or sets the total number of times this strategy was executed.
    /// </summary>
    public long TotalExecutions { get; set; }

    /// <summary>
    /// Gets or sets the number of successful executions.
    /// </summary>
    public long SuccessCount { get; set; }

    /// <summary>
    /// Gets or sets the number of failed executions.
    /// </summary>
    public long FailureCount { get; set; }

    /// <summary>
    /// Gets or sets the total duration of all executions.
    /// </summary>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the average duration of executions.
    /// </summary>
    public TimeSpan AverageDuration =>
        TotalExecutions > 0 ? TimeSpan.FromTicks(TotalDuration.Ticks / TotalExecutions) : TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the minimum duration observed.
    /// </summary>
    public TimeSpan MinDuration { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Gets or sets the maximum duration observed.
    /// </summary>
    public TimeSpan MaxDuration { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last execution.
    /// </summary>
    public DateTime? LastExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the first execution.
    /// </summary>
    public DateTime? FirstExecutionTime { get; set; }

    /// <summary>
    /// Gets or sets the success rate as a percentage (0-100).
    /// </summary>
    public double SuccessRate =>
        TotalExecutions > 0 ? (double)SuccessCount / TotalExecutions * 100 : 0;

    /// <summary>
    /// Gets or sets the failure rate as a percentage (0-100).
    /// </summary>
    public double FailureRate =>
        TotalExecutions > 0 ? (double)FailureCount / TotalExecutions * 100 : 0;

    /// <summary>
    /// Gets or sets the number of times this strategy was used as a fallback.
    /// </summary>
    public long FallbackCount { get; set; }

    /// <summary>
    /// Gets or sets the number of times this strategy was retried.
    /// </summary>
    public long RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the error counts by error type.
    /// </summary>
    public Dictionary<string, long> ErrorCounts { get; set; } = [];

    /// <summary>
    /// Gets or sets the status code distribution.
    /// </summary>
    public Dictionary<int, long> StatusCodeDistribution { get; set; } = [];

    /// <summary>
    /// Gets or sets custom metric values.
    /// </summary>
    public Dictionary<string, object> CustomMetrics { get; set; } = [];

    /// <summary>
    /// Records a successful execution.
    /// </summary>
    /// <param name="duration">The duration of the execution.</param>
    /// <param name="timestamp">The timestamp of the execution.</param>
    /// <param name="statusCode">The HTTP status code if applicable.</param>
    public void RecordSuccess(TimeSpan duration, DateTime timestamp, int? statusCode = null)
    {
        TotalExecutions++;
        SuccessCount++;
        RecordDuration(duration);
        UpdateTimestamps(timestamp);

        if (statusCode.HasValue)
        {
            RecordStatusCode(statusCode.Value);
        }
    }

    /// <summary>
    /// Records a failed execution.
    /// </summary>
    /// <param name="duration">The duration of the execution.</param>
    /// <param name="timestamp">The timestamp of the execution.</param>
    /// <param name="errorType">The type of error that occurred.</param>
    /// <param name="statusCode">The HTTP status code if applicable.</param>
    public void RecordFailure(TimeSpan duration, DateTime timestamp, string? errorType = null, int? statusCode = null)
    {
        TotalExecutions++;
        FailureCount++;
        RecordDuration(duration);
        UpdateTimestamps(timestamp);

        if (!string.IsNullOrEmpty(errorType))
        {
            if (!ErrorCounts.TryAdd(errorType, 1))
            {
                ErrorCounts[errorType]++;
            }
        }

        if (statusCode.HasValue)
        {
            RecordStatusCode(statusCode.Value);
        }
    }

    /// <summary>
    /// Records that this strategy was used as a fallback.
    /// </summary>
    public void RecordFallback()
    {
        FallbackCount++;
    }

    /// <summary>
    /// Records that this strategy was retried.
    /// </summary>
    public void RecordRetry()
    {
        RetryCount++;
    }

    /// <summary>
    /// Resets all metrics to their initial values.
    /// </summary>
    public void Reset()
    {
        TotalExecutions = 0;
        SuccessCount = 0;
        FailureCount = 0;
        TotalDuration = TimeSpan.Zero;
        MinDuration = TimeSpan.MaxValue;
        MaxDuration = TimeSpan.Zero;
        LastExecutionTime = null;
        FirstExecutionTime = null;
        FallbackCount = 0;
        RetryCount = 0;
        ErrorCounts.Clear();
        StatusCodeDistribution.Clear();
        CustomMetrics.Clear();
    }

    /// <summary>
    /// Records the duration of an execution.
    /// </summary>
    private void RecordDuration(TimeSpan duration)
    {
        TotalDuration += duration;

        if (duration < MinDuration)
        {
            MinDuration = duration;
        }

        if (duration > MaxDuration)
        {
            MaxDuration = duration;
        }
    }

    /// <summary>
    /// Updates the first and last execution timestamps.
    /// </summary>
    private void UpdateTimestamps(DateTime timestamp)
    {
        LastExecutionTime = timestamp;

        if (!FirstExecutionTime.HasValue)
        {
            FirstExecutionTime = timestamp;
        }
    }

    /// <summary>
    /// Records a status code occurrence.
    /// </summary>
    private void RecordStatusCode(int statusCode)
    {
        if (!StatusCodeDistribution.TryAdd(statusCode, 1))
        {
            StatusCodeDistribution[statusCode]++;
        }
    }
}
