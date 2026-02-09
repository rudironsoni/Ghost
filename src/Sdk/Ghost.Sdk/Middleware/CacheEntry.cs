using Microsoft.Playwright;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Internal class representing a cached HTTP response with expiration metadata.
/// </summary>
internal sealed class CacheEntry
{
    /// <summary>
    /// Gets the cached HTTP response.
    /// </summary>
    public IResponse Response { get; }

    /// <summary>
    /// Gets the expiration timestamp for this cache entry.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; }

    /// <summary>
    /// Gets a value indicating whether this cache entry has expired.
    /// </summary>
    public bool IsExpired => DateTimeOffset.UtcNow > ExpiresAt;

    /// <summary>
    /// Initializes a new instance of the <see cref="CacheEntry"/> class.
    /// </summary>
    /// <param name="response">The HTTP response to cache.</param>
    /// <param name="expiresAt">The expiration timestamp.</param>
    public CacheEntry(IResponse response, DateTimeOffset expiresAt)
    {
        ArgumentNullException.ThrowIfNull(response);
        Response = response;
        ExpiresAt = expiresAt;
    }
}
