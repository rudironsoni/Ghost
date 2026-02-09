using Microsoft.Playwright;

namespace Ghost.Sdk.Middleware;

/// <summary>
/// Interface for HTTP response caching to avoid redundant requests.
/// </summary>
public interface IHttpCache
{
    /// <summary>
    /// Attempts to retrieve a cached response for the given request.
    /// </summary>
    /// <param name="request">The HTTP request to look up.</param>
    /// <param name="response">The cached response if found; otherwise, null.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if a valid cached response was found; otherwise, false.</returns>
    Task<bool> TryGetAsync(IRequest request, out IResponse? response, CancellationToken ct = default);

    /// <summary>
    /// Stores a response in the cache for the given request.
    /// </summary>
    /// <param name="request">The HTTP request associated with the response.</param>
    /// <param name="response">The HTTP response to cache.</param>
    /// <param name="ttl">Optional time-to-live for the cache entry. If null, uses default TTL.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SetAsync(IRequest request, IResponse response, TimeSpan? ttl = null, CancellationToken ct = default);

    /// <summary>
    /// Invalidates cache entries matching the specified pattern.
    /// </summary>
    /// <param name="pattern">The pattern to match against cache keys (e.g., URL pattern).</param>
    /// <param name="ct">Cancellation token.</param>
    Task InvalidateAsync(string pattern, CancellationToken ct = default);
}
