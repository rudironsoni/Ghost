namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for crawling strategies.
/// </summary>
public sealed class StrategiesConfiguration
{
    /// <summary>
    /// Gets or sets the URL prioritization strategy (FIFO, LIFO, Priority, Custom).
    /// </summary>
    public string Prioritization { get; set; } = "FIFO";

    /// <summary>
    /// Gets or sets the retry strategy configuration.
    /// </summary>
    public RetryStrategyConfiguration Retry { get; set; } = new();

    /// <summary>
    /// Gets or sets the deduplication strategy (Url, Content, ContentHash).
    /// </summary>
    public string Deduplication { get; set; } = "Url";

    /// <summary>
    /// Gets or sets the content hash algorithm for deduplication (MD5, SHA256).
    /// </summary>
    public string ContentHashAlgorithm { get; set; } = "SHA256";

    /// <summary>
    /// Gets or sets the error handling strategy (Skip, Retry, Fail).
    /// </summary>
    public string ErrorHandling { get; set; } = "Retry";

    /// <summary>
    /// Gets or sets rate limiting configuration.
    /// </summary>
    public RateLimitConfiguration RateLimit { get; set; } = new();

    /// <summary>
    /// Gets or sets caching configuration.
    /// </summary>
    public CachingConfiguration Caching { get; set; } = new();
}

/// <summary>
/// Configuration for retry strategy.
/// </summary>
public sealed class RetryStrategyConfiguration
{
    /// <summary>
    /// Gets or sets whether retry is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of retry attempts.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the backoff strategy (Fixed, Linear, Exponential).
    /// </summary>
    public string BackoffStrategy { get; set; } = "Exponential";

    /// <summary>
    /// Gets or sets the initial delay (milliseconds).
    /// </summary>
    public int InitialDelay { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the maximum delay (milliseconds).
    /// </summary>
    public int MaxDelay { get; set; } = 30000;

    /// <summary>
    /// Gets or sets HTTP status codes that should trigger a retry.
    /// </summary>
    public List<int> RetryStatusCodes { get; set; } = new() { 429, 500, 502, 503, 504 };
}

/// <summary>
/// Configuration for rate limiting.
/// </summary>
public sealed class RateLimitConfiguration
{
    /// <summary>
    /// Gets or sets whether rate limiting is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum requests per second.
    /// </summary>
    public double RequestsPerSecond { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the burst size (max requests in burst).
    /// </summary>
    public int BurstSize { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum concurrent requests.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets per-domain rate limiting configuration.
    /// </summary>
    public Dictionary<string, DomainRateLimitConfiguration> PerDomain { get; set; } = new();
}

/// <summary>
/// Configuration for per-domain rate limiting.
/// </summary>
public sealed class DomainRateLimitConfiguration
{
    /// <summary>
    /// Gets or sets the maximum requests per second for this domain.
    /// </summary>
    public double RequestsPerSecond { get; set; } = 1.0;

    /// <summary>
    /// Gets or sets the maximum concurrent requests for this domain.
    /// </summary>
    public int MaxConcurrency { get; set; } = 1;
}

/// <summary>
/// Configuration for response caching.
/// </summary>
public sealed class CachingConfiguration
{
    /// <summary>
    /// Gets or sets whether caching is enabled.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the cache provider (Memory, Redis, Disk).
    /// </summary>
    public string Provider { get; set; } = "Memory";

    /// <summary>
    /// Gets or sets the cache TTL (seconds).
    /// </summary>
    public int TtlSeconds { get; set; } = 3600;

    /// <summary>
    /// Gets or sets the Redis connection string (for Redis provider).
    /// </summary>
    public string? RedisConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the disk cache directory (for Disk provider).
    /// </summary>
    public string? DiskCacheDirectory { get; set; }
}
