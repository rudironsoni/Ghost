using System.Net;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Internal class representing a cached DNS resolution result with expiration metadata.
/// </summary>
internal sealed class DnsCacheEntry
{
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Gets the cached IP addresses for the hostname.
    /// </summary>
    public IPAddress[] Addresses { get; }

    /// <summary>
    /// Gets the expiration timestamp for this DNS cache entry.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets a value indicating whether this DNS cache entry has expired.
    /// </summary>
    public bool IsExpired => _timeProvider.GetUtcNow() >= ExpiresAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="DnsCacheEntry"/> class.
    /// </summary>
    /// <param name="addresses">The IP addresses to cache.</param>
    /// <param name="expiresAt">The expiration timestamp.</param>
    /// <param name="timeProvider">The time provider for expiration checks.</param>
    public DnsCacheEntry(IPAddress[] addresses, DateTimeOffset expiresAt, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        ArgumentNullException.ThrowIfNull(timeProvider);
        Addresses = addresses;
        ExpiresAt = expiresAt;
        _timeProvider = timeProvider;
    }
}
