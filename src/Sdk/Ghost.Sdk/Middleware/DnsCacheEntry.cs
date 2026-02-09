using System.Net;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Internal class representing a cached DNS resolution result with expiration metadata.
/// </summary>
internal sealed class DnsCacheEntry
{
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
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="DnsCacheEntry"/> class.
    /// </summary>
    /// <param name="addresses">The IP addresses to cache.</param>
    /// <param name="expiresAt">The expiration timestamp.</param>
    public DnsCacheEntry(IPAddress[] addresses, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(addresses);
        Addresses = addresses;
        ExpiresAt = expiresAt;
    }
}
