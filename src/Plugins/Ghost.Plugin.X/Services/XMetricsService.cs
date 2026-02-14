using System.Collections.Concurrent;

namespace Ghost.Plugin.X.Services;

/// <summary>
/// Service for tracking X platform metrics and analytics.
/// </summary>
public interface IXMetricsService
{
    void RecordPost(string postId, DateTime timestamp, bool success, TimeSpan duration);
    void RecordError(string errorCode, string operation);
    void RecordRateLimit(TimeSpan retryAfter);
    XMetrics GetMetrics();
    void Reset();
}

/// <summary>
/// Implementation of X metrics service.
/// </summary>
public class XMetricsService : IXMetricsService
{
    private readonly ConcurrentDictionary<string, PostMetric> _posts = new();
    private readonly ConcurrentDictionary<string, int> _errors = new();
    private long _totalRequests;
    private long _successfulRequests;
    private long _failedRequests;
    private long _rateLimitHits;
    private DateTime? _firstRequestAt;
    private DateTime _lastRequestAt;

    public void RecordPost(string postId, DateTime timestamp, bool success, TimeSpan duration)
    {
        _posts[postId] = new PostMetric
        {
            PostId = postId,
            Timestamp = timestamp,
            Success = success,
            Duration = duration
        };

        Interlocked.Increment(ref _totalRequests);
        if (success)
        {
            Interlocked.Increment(ref _successfulRequests);
        }
        else
        {
            Interlocked.Increment(ref _failedRequests);
        }

        _firstRequestAt ??= timestamp;
        _lastRequestAt = timestamp;
    }

    public void RecordError(string errorCode, string operation)
    {
        _errors.AddOrUpdate(errorCode, 1, (_, count) => count + 1);
    }

    public void RecordRateLimit(TimeSpan retryAfter)
    {
        Interlocked.Increment(ref _rateLimitHits);
    }

    public XMetrics GetMetrics()
    {
        var posts = _posts.Values.ToList();

        return new XMetrics
        {
            TotalRequests = _totalRequests,
            SuccessfulRequests = _successfulRequests,
            FailedRequests = _failedRequests,
            SuccessRate = _totalRequests > 0 ? (double)_successfulRequests / _totalRequests : 0,
            RateLimitHits = _rateLimitHits,
            AverageRequestDuration = posts.Any(p => p.Success)
                ? TimeSpan.FromMilliseconds(posts.Where(p => p.Success).Average(p => p.Duration.TotalMilliseconds))
                : TimeSpan.Zero,
            TotalPosts = posts.Count,
            FirstRequestAt = _firstRequestAt,
            LastRequestAt = _lastRequestAt,
            ErrorCounts = new Dictionary<string, int>(_errors),
            RecentPosts = posts.OrderByDescending(p => p.Timestamp).Take(10).ToList()
        };
    }

    public void Reset()
    {
        _posts.Clear();
        _errors.Clear();
        _totalRequests = 0;
        _successfulRequests = 0;
        _failedRequests = 0;
        _rateLimitHits = 0;
        _firstRequestAt = null;
        _lastRequestAt = default;
    }
}

/// <summary>
/// Individual post metric.
/// </summary>
public class PostMetric
{
    public string PostId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public bool Success { get; set; }
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Aggregated X metrics.
/// </summary>
public class XMetrics
{
    public long TotalRequests { get; set; }
    public long SuccessfulRequests { get; set; }
    public long FailedRequests { get; set; }
    public double SuccessRate { get; set; }
    public long RateLimitHits { get; set; }
    public TimeSpan AverageRequestDuration { get; set; }
    public int TotalPosts { get; set; }
    public DateTime? FirstRequestAt { get; set; }
    public DateTime LastRequestAt { get; set; }
    public Dictionary<string, int> ErrorCounts { get; set; } = new();
    public List<PostMetric> RecentPosts { get; set; } = new();
}
