namespace Ghost.Monitoring;

/// <summary>
/// Snapshot of monitoring metrics.
/// </summary>
public sealed class MetricsSnapshot
{
    /// <summary>
    /// Timestamp of the snapshot.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Total requests observed.
    /// </summary>
    public long TotalRequests { get; init; }
}
