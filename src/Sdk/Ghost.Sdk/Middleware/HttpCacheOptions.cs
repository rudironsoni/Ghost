namespace Ghost.Sdk.Middleware;

/// <summary>
/// Configuration options for HTTP response caching.
/// </summary>
public class HttpCacheOptions
{
    /// <summary>
    /// Default time-to-live for cache entries.
    /// </summary>
    public TimeSpan DefaultTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Maximum cache size in bytes (default: 100MB).
    /// </summary>
    public long MaxCacheSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Interval for cleaning up expired cache entries.
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the time provider for cache operations.
    /// </summary>
    public TimeProvider TimeProvider { get; set; } = TimeProvider.System;
}
