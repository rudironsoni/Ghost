namespace Ghost.Worker;

/// <summary>
/// Configuration for the Ghost Worker.
/// </summary>
public sealed class WorkerConfiguration
{
    /// <summary>
    /// Gets or sets the unique worker identifier.
    /// </summary>
    public string WorkerId { get; set; } = Environment.MachineName;

    /// <summary>
    /// Gets or sets the Kubernetes node name (if running in k8s).
    /// </summary>
    public string NodeName { get; set; } = Environment.MachineName;

    /// <summary>
    /// Gets or sets the Redis connection string.
    /// </summary>
    public string RedisConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Gets or sets the Redis queue key name.
    /// </summary>
    public string RedisQueueKey { get; set; } = "ghost:jobs:queue";

    /// <summary>
    /// Gets or sets the maximum number of concurrent jobs to process.
    /// </summary>
    public int MaxConcurrentJobs { get; set; } = 5;

    /// <summary>
    /// Gets or sets the poll interval in milliseconds when queue is empty.
    /// </summary>
    public int PollIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the results expiration time in hours.
    /// </summary>
    public int ResultsExpirationHours { get; set; } = 24;
}
