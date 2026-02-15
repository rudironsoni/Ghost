using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Ghost.Kernel.Monitoring;

public interface IMetricsCollector
{
    public void RecordScrapeAttempt(string platform);
    public void RecordScrapeSuccess(string platform);
    public void RecordScrapeFailure(string platform);
    public void RecordCacheHit(string platform);
    public void RecordCacheMiss(string platform);
    public void RecordCircuitBreakerState(string platform, string state);
    public MetricsSnapshot GetSnapshot();
    public void Reset();
}

public class MetricsCollector : IMetricsCollector
{
    private readonly ConcurrentDictionary<string, PlatformMetrics> _metrics = new();
    private readonly object _lock = new();

    public void RecordScrapeAttempt(string platform)
    {
        PlatformMetrics pm = _metrics.GetOrAdd(platform, _ => new PlatformMetrics());
        Interlocked.Increment(ref pm.ScrapeAttempts);
    }

    public void RecordScrapeSuccess(string platform)
    {
        PlatformMetrics pm = _metrics.GetOrAdd(platform, _ => new PlatformMetrics());
        Interlocked.Increment(ref pm.ScrapeSuccesses);
    }

    public void RecordScrapeFailure(string platform)
    {
        PlatformMetrics pm = _metrics.GetOrAdd(platform, _ => new PlatformMetrics());
        Interlocked.Increment(ref pm.ScrapeFailures);
    }

    public void RecordCacheHit(string platform)
    {
        PlatformMetrics pm = _metrics.GetOrAdd(platform, _ => new PlatformMetrics());
        Interlocked.Increment(ref pm.CacheHits);
    }

    public void RecordCacheMiss(string platform)
    {
        PlatformMetrics pm = _metrics.GetOrAdd(platform, _ => new PlatformMetrics());
        Interlocked.Increment(ref pm.CacheMisses);
    }

    public void RecordCircuitBreakerState(string platform, string state)
    {
        PlatformMetrics pm = _metrics.GetOrAdd(platform, _ => new PlatformMetrics());
        lock (_lock)
        {
            pm.CircuitBreakerState = state;
        }
    }

    public MetricsSnapshot GetSnapshot()
    {
        var platforms = new Dictionary<string, PlatformMetricsSnapshot>();
        foreach ((string? platform, PlatformMetrics? metrics) in _metrics)
        {
            platforms[platform] = new PlatformMetricsSnapshot
            {
                ScrapeAttempts = Interlocked.Read(ref metrics.ScrapeAttempts),
                ScrapeSuccesses = Interlocked.Read(ref metrics.ScrapeSuccesses),
                ScrapeFailures = Interlocked.Read(ref metrics.ScrapeFailures),
                CacheHits = Interlocked.Read(ref metrics.CacheHits),
                CacheMisses = Interlocked.Read(ref metrics.CacheMisses),
                CircuitBreakerState = metrics.CircuitBreakerState
            };
        }

        return new MetricsSnapshot
        {
            Timestamp = DateTime.UtcNow,
            Platforms = platforms
        };
    }

    public void Reset()
    {
        _metrics.Clear();
    }
}

#pragma warning disable CA1051
public class PlatformMetrics
{
    public long ScrapeAttempts;
    public long ScrapeSuccesses;
    public long ScrapeFailures;
    public long CacheHits;
    public long CacheMisses;
    public string CircuitBreakerState = "Closed";
}
#pragma warning restore CA1051

public class MetricsSnapshot
{
    public DateTime Timestamp { get; set; }
    public Dictionary<string, PlatformMetricsSnapshot> Platforms { get; set; } = new();
}

public class PlatformMetricsSnapshot
{
    public long ScrapeAttempts { get; set; }
    public long ScrapeSuccesses { get; set; }
    public long ScrapeFailures { get; set; }
    public long CacheHits { get; set; }
    public long CacheMisses { get; set; }
    public string CircuitBreakerState { get; set; } = "Closed";
}
