using System.Text.Json.Serialization;

namespace Ghost.Queue;

/// <summary>
/// Represents the result of a completed job
/// </summary>
public sealed class JobResult
{
    /// <summary>
    /// Job identifier
    /// </summary>
    [JsonPropertyName("job_id")]
    public string JobId { get; set; } = string.Empty;

    /// <summary>
    /// Whether the job succeeded
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    /// <summary>
    /// Result data (JSON serialized)
    /// </summary>
    [JsonPropertyName("result_data")]
    public string? ResultData { get; set; }

    /// <summary>
    /// Error message if job failed
    /// </summary>
    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time when job was completed
    /// </summary>
    [JsonPropertyName("completed_at")]
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Worker ID that processed this job
    /// </summary>
    [JsonPropertyName("worker_id")]
    public string? WorkerId { get; set; }

    /// <summary>
    /// Processing duration in milliseconds
    /// </summary>
    [JsonPropertyName("duration_ms")]
    public long DurationMs { get; set; }
}
