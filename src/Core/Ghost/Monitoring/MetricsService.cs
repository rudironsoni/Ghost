namespace Ghost.Monitoring;

/// <summary>
/// Provides basic metrics tracking.
/// </summary>
public sealed class MetricsService
{
    private long _totalRequests;

    /// <summary>
    /// Records a single request.
    /// </summary>
    public void RecordRequest()
    {
        System.Threading.Interlocked.Increment(ref _totalRequests);
    }

    /// <summary>
    /// Returns a snapshot of current metrics.
    /// </summary>
    public MetricsSnapshot GetSnapshot()
    {
        return new MetricsSnapshot
        {
            TotalRequests = System.Threading.Interlocked.Read(ref _totalRequests)
        };
    }
}
