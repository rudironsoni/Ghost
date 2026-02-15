using System.Collections.Concurrent;

namespace Ghost.Sdk.Statistics;

/// <summary>
/// Thread-safe implementation of spider statistics collection.
/// </summary>
/// <remarks>
/// This implementation uses concurrent collections and atomic operations to safely
/// collect statistics from multiple spiders running in parallel. Latency tracking
/// uses a lock for list operations to ensure accurate averaging.
/// </remarks>
public sealed class StatsCollector : IStatsCollector
{
    // Internal class to hold counters as fields for Interlocked operations
    private sealed class StatsCounters
    {
        public long RequestCount;
        public long ResponseCount;
        public long ErrorCount;
        public long ItemCount;
    }

    private readonly ConcurrentDictionary<string, StatsCounters> _counters = new();
    private readonly ConcurrentDictionary<string, SpiderStats> _stats = new();
    private readonly ConcurrentDictionary<string, List<TimeSpan>> _latencies = new();
    private readonly ConcurrentDictionary<string, object> _latencyLocks = new();

    /// <summary>
    /// Records that a request was initiated by the spider.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider making the request.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId is null.</exception>
    /// <remarks>
    /// Initializes tracking for the spider if this is the first request. Sets the start
    /// time and increments the request counter atomically.
    /// </remarks>
    public void RecordRequest(string spiderId)
    {
        ArgumentNullException.ThrowIfNull(spiderId);

        SpiderStats stats = _stats.GetOrAdd(spiderId, _ => new SpiderStats
        {
            SpiderId = spiderId,
            StartTime = DateTimeOffset.UtcNow
        });

        StatsCounters counters = _counters.GetOrAdd(spiderId, _ => new StatsCounters());
        long newCount = Interlocked.Increment(ref counters.RequestCount);
        stats.RequestCount = newCount;
    }

    /// <summary>
    /// Records a received response with its status code and latency.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider that received the response.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="latency">The time taken to receive the response.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when spider has not been initialized via RecordRequest.</exception>
    /// <remarks>
    /// Updates response count, status code distribution, average response time, and total duration.
    /// Thread-safe for concurrent access from multiple spiders.
    /// </remarks>
    public void RecordResponse(string spiderId, int statusCode, TimeSpan latency)
    {
        ArgumentNullException.ThrowIfNull(spiderId);

        if (!_stats.TryGetValue(spiderId, out SpiderStats? stats))
        {
            throw new InvalidOperationException($"Spider '{spiderId}' has not been initialized. Call RecordRequest first.");
        }

        if (!_counters.TryGetValue(spiderId, out StatsCounters? counters))
        {
            throw new InvalidOperationException($"Spider '{spiderId}' has not been initialized. Call RecordRequest first.");
        }

        long newCount = Interlocked.Increment(ref counters.ResponseCount);
        stats.ResponseCount = newCount;

        // Update status code distribution atomically
        stats.StatusCodeDistribution.AddOrUpdate(statusCode, 1, (_, count) => count + 1);

        // Update latency tracking with locking for list operations
        List<TimeSpan> latencies = _latencies.GetOrAdd(spiderId, _ => new List<TimeSpan>());
        object lockObj = _latencyLocks.GetOrAdd(spiderId, _ => new object());

        lock (lockObj)
        {
            latencies.Add(latency);
            stats.AverageResponseTime = latencies.Average(l => l.TotalMilliseconds);
        }

        // Update total duration
        stats.TotalDuration = DateTimeOffset.UtcNow - stats.StartTime;
    }

    /// <summary>
    /// Records an error that occurred during spider execution.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider that encountered the error.</param>
    /// <param name="ex">The exception that was thrown.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId or ex is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when spider has not been initialized via RecordRequest.</exception>
    /// <remarks>
    /// Increments the error counter atomically. The exception details are not stored to avoid
    /// memory leaks in long-running spiders.
    /// </remarks>
    public void RecordError(string spiderId, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        ArgumentNullException.ThrowIfNull(ex);

        if (!_stats.TryGetValue(spiderId, out SpiderStats? stats))
        {
            throw new InvalidOperationException($"Spider '{spiderId}' has not been initialized. Call RecordRequest first.");
        }

        if (!_counters.TryGetValue(spiderId, out StatsCounters? counters))
        {
            throw new InvalidOperationException($"Spider '{spiderId}' has not been initialized. Call RecordRequest first.");
        }

        long newCount = Interlocked.Increment(ref counters.ErrorCount);
        stats.ErrorCount = newCount;
    }

    /// <summary>
    /// Records that an item was successfully scraped and processed.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider that scraped the item.</param>
    /// <param name="itemType">The type/category of the scraped item.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId or itemType is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown when spider has not been initialized via RecordRequest.</exception>
    /// <remarks>
    /// Increments the item counter atomically. The itemType parameter is for future extensibility
    /// but is not currently stored or tracked separately.
    /// </remarks>
    public void RecordItem(string spiderId, string itemType)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        ArgumentNullException.ThrowIfNull(itemType);

        if (!_stats.TryGetValue(spiderId, out SpiderStats? stats))
        {
            throw new InvalidOperationException($"Spider '{spiderId}' has not been initialized. Call RecordRequest first.");
        }

        if (!_counters.TryGetValue(spiderId, out StatsCounters? counters))
        {
            throw new InvalidOperationException($"Spider '{spiderId}' has not been initialized. Call RecordRequest first.");
        }

        long newCount = Interlocked.Increment(ref counters.ItemCount);
        stats.ItemCount = newCount;
    }

    /// <summary>
    /// Gets the current statistics for a specific spider.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider.</param>
    /// <returns>
    /// A <see cref="SpiderStats"/> instance containing current metrics, or a new empty
    /// instance if the spider has not been tracked yet.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when spiderId is null.</exception>
    /// <remarks>
    /// Returns a reference to the actual stats object, not a copy. The caller should not
    /// modify the returned object directly.
    /// </remarks>
    public SpiderStats GetStats(string spiderId)
    {
        ArgumentNullException.ThrowIfNull(spiderId);

        return _stats.TryGetValue(spiderId, out SpiderStats? stats)
            ? stats
            : new SpiderStats { SpiderId = spiderId };
    }

    /// <summary>
    /// Gets the current statistics for all tracked spiders.
    /// </summary>
    /// <returns>
    /// A dictionary mapping spider IDs to their statistics. Returns an empty dictionary
    /// if no spiders have been tracked yet.
    /// </returns>
    /// <remarks>
    /// Returns a new dictionary containing references to the stats objects. The dictionary
    /// itself is a snapshot, but the stats objects it contains are the live instances.
    /// </remarks>
    public Dictionary<string, SpiderStats> GetAllStats()
    {
        return new Dictionary<string, SpiderStats>(_stats);
    }
}
