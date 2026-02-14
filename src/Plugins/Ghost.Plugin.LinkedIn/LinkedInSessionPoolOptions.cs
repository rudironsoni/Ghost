using System;

namespace Ghost.Plugin.LinkedIn;

/// <summary>
/// Options for configuring the LinkedIn browser session pool.
/// </summary>
public class LinkedInSessionPoolOptions
{
    /// <summary>
    /// Gets or sets the maximum number of sessions the pool may hold.
    /// </summary>
    public int MaxSize { get; set; } = 20;

    /// <summary>
    /// Gets or sets the number of sessions to pre-create during warmup.
    /// </summary>
    public int WarmCount { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum idle time before a session is recycled.
    /// </summary>
    public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum lifetime for a session before it is recycled.
    /// </summary>
    public TimeSpan MaxLifetime { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Gets or sets the interval for periodic health checks.
    /// </summary>
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
}
