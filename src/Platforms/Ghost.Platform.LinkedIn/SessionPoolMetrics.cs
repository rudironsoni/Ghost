using System;

namespace Ghost.Platform.LinkedIn;

/// <summary>
/// Metrics captured by the LinkedIn session pool.
/// </summary>
public class SessionPoolMetrics
{
    /// <summary>
    /// Gets or sets the number of available sessions in the pool.
    /// </summary>
    public int AvailableCount { get; set; }

    /// <summary>
    /// Gets or sets the number of sessions currently in use.
    /// </summary>
    public int InUseCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of sessions created by the pool.
    /// </summary>
    public int TotalCreated { get; set; }

    /// <summary>
    /// Gets or sets the total number of sessions recycled back into the pool.
    /// </summary>
    public int TotalRecycled { get; set; }

    /// <summary>
    /// Gets or sets the total number of sessions disposed by the pool.
    /// </summary>
    public int TotalDisposed { get; set; }

    /// <summary>
    /// Gets or sets the average acquisition time for sessions.
    /// </summary>
    public TimeSpan AverageAcquisitionTime { get; set; }

    /// <summary>
    /// Gets or sets the timestamp of the last health check.
    /// </summary>
    public DateTime LastHealthCheck { get; set; }
}
