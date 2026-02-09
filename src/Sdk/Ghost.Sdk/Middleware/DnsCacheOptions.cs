namespace Ghost.Sdk.Middleware;

/// <summary>
/// Configuration options for DNS caching.
/// </summary>
public class DnsCacheOptions
{
    /// <summary>
    /// Gets or sets the time-to-live for DNS cache entries.
    /// </summary>
    public TimeSpan Ttl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the maximum number of entries to keep in the cache.
    /// </summary>
    public int MaxEntries { get; set; } = 1000;
}
