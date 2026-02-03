# Ghost 50K Scale - Interface Contracts

**Version**: 1.0  
**Date**: 2026-02-02  
**Status**: ACTIVE

This document defines the interface contracts that all agents must implement to ensure interoperability.

---

## 1. Circuit Breaker Interface (Agent 4)

```csharp
namespace Ghost.Core.Resilience;

public interface ICircuitBreaker
{
    /// <summary>
    /// Executes the action within circuit breaker protection
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action);
    
    /// <summary>
    /// Current state of the circuit
    /// </summary>
    CircuitState State { get; }
    
    /// <summary>
    /// Platform identifier (LinkedIn, Indeed, Proxy)
    /// </summary>
    string Platform { get; }
    
    /// <summary>
    /// Event raised when circuit state changes
    /// </summary>
    event EventHandler<CircuitStateChangedEventArgs>? StateChanged;
    
    /// <summary>
    /// Gets current metrics
    /// </summary>
    CircuitBreakerMetrics GetMetrics();
}

public enum CircuitState
{
    Closed,    // Normal operation
    Open,      // Failing fast
    HalfOpen   // Testing recovery
}

public class CircuitStateChangedEventArgs : EventArgs
{
    public CircuitState OldState { get; set; }
    public CircuitState NewState { get; set; }
    public string Platform { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

public class CircuitBreakerMetrics
{
    public int FailureCount { get; set; }
    public int SuccessCount { get; set; }
    public DateTime LastFailure { get; set; }
    public DateTime LastSuccess { get; set; }
    public TimeSpan TimeInCurrentState { get; set; }
}
```

---

## 2. Retry Policy Interface (Agent 5)

```csharp
namespace Ghost.Core.Resilience;

public interface IRetryPolicy
{
    /// <summary>
    /// Executes action with retry logic
    /// </summary>
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, Func<Exception, bool> isRetryable);
    
    /// <summary>
    /// Retry configuration options
    /// </summary>
    RetryPolicyOptions Options { get; }
    
    /// <summary>
    /// Current attempt number (0 = first attempt)
    /// </summary>
    int CurrentAttempt { get; }
}

public class RetryPolicyOptions
{
    public int MaxRetries { get; set; } = 3;
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(30);
    public bool UseJitter { get; set; } = true;
}

public static class RetryableErrorClassifier
{
    public static bool IsRetryable(HttpStatusCode code) => code switch
    {
        HttpStatusCode.TooManyRequests => true,      // 429
        HttpStatusCode.ServiceUnavailable => true,   // 503
        HttpStatusCode.GatewayTimeout => true,       // 504
        _ => false
    };
    
    public static bool IsRetryable(Exception ex) => ex switch
    {
        TaskCanceledException => true,
        HttpRequestException => true,
        TimeoutException => true,
        _ => false
    };
}
```

---

## 3. Dead Letter Queue Interface (Agent 6)

```csharp
namespace Ghost.Core.Resilience;

public interface IDeadLetterQueue
{
    Task EnqueueAsync(FailedScrapeJob job);
    Task<IReadOnlyList<FailedScrapeJob>> GetFailedJobsAsync(TimeSpan since);
    Task<IReadOnlyList<FailedScrapeJob>> GetFailedJobsByPlatformAsync(string platform, TimeSpan since);
    Task<FailedScrapeJob?> GetJobAsync(string jobId);
    Task RetryAsync(string jobId);
    Task RetryAllAsync(TimeSpan since);
    Task ArchiveAsync(string jobId);
    Task ArchiveAllAsync(TimeSpan olderThan);
    Task<int> GetQueueDepthAsync();
    string StoragePath { get; }
}

public class FailedScrapeJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Platform { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string StackTrace { get; set; } = string.Empty;
    public int RetryCount { get; set; }
    public DateTime FailedAt { get; set; }
    public DateTime? LastRetryAt { get; set; }
    public string? ProxyUsed { get; set; }
    public string? CircuitState { get; set; }
    public Dictionary<string, object>? Metadata { get; set; }
}
```

---

## 4. Caching Interface (Agent 9)

```csharp
namespace Ghost.Core.Caching;

public interface IScrapeCache
{
    Task<IReadOnlyList<JobListing>?> GetSearchResultsAsync(
        string platform, string query, string location);
    
    Task SetSearchResultsAsync(
        string platform, string query, string location, 
        IReadOnlyList<JobListing> jobs, TimeSpan ttl);
    
    Task<JobListing?> GetJobDetailsAsync(string jobId);
    Task SetJobDetailsAsync(string jobId, JobListing job, TimeSpan ttl);
    
    Task InvalidateAsync(string platform, string query, string location);
    Task InvalidatePlatformAsync(string platform);
    Task<CacheStats> GetStatsAsync();
}

public class CacheStats
{
    public long MemoryHits { get; set; }
    public long DiskHits { get; set; }
    public long Misses { get; set; }
    public long MemorySize { get; set; }
    public long DiskSize { get; set; }
    public double HitRate => (MemoryHits + DiskHits) / (double)(MemoryHits + DiskHits + Misses);
}
```

---

## 5. Proxy Health Interface (Agent 2)

```csharp
namespace Ghost.Core.Proxy;

public interface IProxyHealthChecker
{
    Task<ProxyHealthReport> CheckAllProxiesAsync();
    Task<ProxyStatus> CheckProxyAsync(string proxyUrl);
    Task<ProxyLatencyResult> MeasureLatencyAsync(string proxyUrl);
}

public class ProxyHealthReport
{
    public List<ProxyStatus> Proxies { get; set; } = new();
    public int HealthyCount => Proxies.Count(p => p.IsHealthy);
    public int UnhealthyCount => Proxies.Count(p => !p.IsHealthy);
    public List<string> GetHealthyProxiesSortedByLatency() => 
        Proxies.Where(p => p.IsHealthy)
               .OrderBy(p => p.LatencyMs)
               .Select(p => p.Url)
               .ToList();
}

public class ProxyStatus
{
    public string Url { get; set; } = string.Empty;
    public bool IsHealthy { get; set; }
    public long LatencyMs { get; set; }
    public string? Error { get; set; }
    public DateTime LastChecked { get; set; }
}

public class ProxyLatencyResult
{
    public string Url { get; set; } = string.Empty;
    public long LatencyMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}
```

---

## 6. Session Pool Interface (Agent 7)

```csharp
namespace Ghost.Platform.LinkedIn;

public interface ILinkedInSessionPool : IDisposable
{
    Task<IBrowserSession> AcquireAsync(CancellationToken ct = default);
    void Release(IBrowserSession session);
    Task WarmupAsync(int count, CancellationToken ct = default);
    Task PruneAsync(CancellationToken ct = default);
    SessionPoolMetrics GetMetrics();
}

public class LinkedInSessionPoolOptions
{
    public int MaxSize { get; set; } = 20;
    public int WarmCount { get; set; } = 5;
    public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan MaxLifetime { get; set; } = TimeSpan.FromHours(1);
    public TimeSpan HealthCheckInterval { get; set; } = TimeSpan.FromMinutes(5);
}

public class SessionPoolMetrics
{
    public int AvailableCount { get; set; }
    public int InUseCount { get; set; }
    public int TotalCreated { get; set; }
    public int TotalRecycled { get; set; }
    public int TotalDisposed { get; set; }
    public TimeSpan AverageAcquisitionTime { get; set; }
    public DateTime LastHealthCheck { get; set; }
}
```

---

## Integration Points

### Dependency Flow
```
┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐
│  Agent 1        │────▶│  GuestJobSearch │◄────│  Agent 7        │
│  (QueryBuilder) │     │                 │     │  (SessionPool)  │
└─────────────────┘     └─────────────────┘     └─────────────────┘
         │                                               │
         │              ┌─────────────────┐              │
         └─────────────▶│  Agent 4/5      │◄─────────────┘
                        │ (Circuit/Retry) │
                        └─────────────────┘
                                 │
                        ┌─────────────────┐
                        │  Agent 6        │
                        │  (DLQ)          │
                        └─────────────────┘
```

### Configuration Requirements

All agents must read configuration from `appsettings.json`:

```json
{
  "Ghost": {
    "CircuitBreaker": {
      "LinkedIn": { "FailureThreshold": 5, "Timeout": "00:05:00" },
      "Indeed": { "FailureThreshold": 10, "Timeout": "00:03:00" }
    },
    "Retry": {
      "MaxRetries": 3,
      "BaseDelay": "00:00:01",
      "MaxDelay": "00:00:30"
    },
    "Cache": {
      "MemoryTtlMinutes": 60,
      "DiskTtlHours": 24,
      "Path": "/var/ghost/cache"
    },
    "DLQ": {
      "Path": "/var/ghost/dlq",
      "ArchiveAfterDays": 7
    },
    "SessionPool": {
      "MaxSize": 20,
      "WarmCount": 5
    }
  }
}
```

---

## Testing Requirements

Each agent must provide:
- Unit tests (80%+ coverage)
- Integration tests where applicable
- Performance benchmarks for critical paths

## Acceptance Criteria

- [ ] All interfaces compile without errors
- [ ] All implementations pass unit tests
- [ ] Integration tests pass
- [ ] No circular dependencies
- [ ] Thread-safe implementations
- [ ] XML documentation complete
