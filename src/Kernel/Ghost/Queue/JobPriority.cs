namespace Ghost.Queue;

/// <summary>
/// Job priority levels for queue processing
/// </summary>
public enum JobPriority
{
    /// <summary>
    /// P0: Critical (platform health checks)
    /// </summary>
    Critical = 0,

    /// <summary>
    /// P1: High (user-initiated searches)
    /// </summary>
    High = 1,

    /// <summary>
    /// P2: Normal (scheduled jobs)
    /// </summary>
    Normal = 2,

    /// <summary>
    /// P3: Low (background tasks)
    /// </summary>
    Low = 3
}
