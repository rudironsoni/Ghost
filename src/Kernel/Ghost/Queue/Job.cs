using System.Text.Json.Serialization;

namespace Ghost.Queue;

/// <summary>
/// Represents a job in the queue system
/// </summary>
public sealed class Job
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="Job"/> class.
    /// </summary>
    /// <param name="timeProvider">The time provider to use for time-based operations. Defaults to system time.</param>
    public Job(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Unique job identifier
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Job type identifier (e.g., "linkedin:search", "indeed:getdetails")
    /// </summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Job payload (JSON serialized parameters)
    /// </summary>
    [JsonPropertyName("payload")]
    public string Payload { get; set; } = string.Empty;

    /// <summary>
    /// Job priority
    /// </summary>
    [JsonPropertyName("priority")]
    public JobPriority Priority { get; set; } = JobPriority.Normal;

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    [JsonPropertyName("retry_count")]
    public int RetryCount { get; set; }

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    [JsonPropertyName("max_retries")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Time when job was created
    /// </summary>
    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Time when job was last attempted
    /// </summary>
    [JsonPropertyName("last_attempt_at")]
    public DateTime? LastAttemptAt { get; set; }

    /// <summary>
    /// Worker ID that is currently processing this job
    /// </summary>
    [JsonPropertyName("worker_id")]
    public string? WorkerId { get; set; }

    /// <summary>
    /// Last error message (if any)
    /// </summary>
    [JsonPropertyName("last_error")]
    public string? LastError { get; set; }
}
