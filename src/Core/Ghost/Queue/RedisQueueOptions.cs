namespace Ghost.Queue;

/// <summary>
/// Configuration options for Redis job queue
/// </summary>
public sealed class RedisQueueOptions
{
    /// <summary>
    /// Redis connection string
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Redis database number
    /// </summary>
    public int Database { get; set; } = 0;

    /// <summary>
    /// Queue key prefix
    /// </summary>
    public string QueuePrefix { get; set; } = "ghost:jobs";

    /// <summary>
    /// Maximum retries for failed jobs
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Maximum completed jobs to keep in history
    /// </summary>
    public int MaxCompletedHistory { get; set; } = 10000;

    /// <summary>
    /// Job timeout in seconds (mark stale jobs as failed)
    /// </summary>
    public int JobTimeoutSeconds { get; set; } = 300;
}
