namespace Ghost.Sdk.Middleware;

/// <summary>
/// Interface for robots.txt middleware that respects crawling rules.
/// </summary>
public interface IRobotsMiddleware
{
    /// <summary>
    /// Determines whether a URL can be fetched based on robots.txt rules.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <param name="userAgent">The user-agent string to match against rules.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the URL can be fetched, false otherwise.</returns>
    public Task<bool> CanFetchAsync(string url, string userAgent, CancellationToken ct = default);

    /// <summary>
    /// Loads and caches the robots.txt file for a given base URL.
    /// </summary>
    /// <param name="baseUrl">The base URL (e.g., https://example.com).</param>
    /// <param name="userAgent">The user-agent string for fetching.</param>
    /// <param name="ct">Cancellation token.</param>
    public Task LoadRobotsTxtAsync(string baseUrl, string userAgent, CancellationToken ct = default);
}
