namespace Ghost.Sdk.Middleware;

/// <summary>
/// Interface for offsite middleware that filters external domain URLs.
/// </summary>
public interface IOffsiteMiddleware
{
    /// <summary>
    /// Determines whether a URL should be followed based on the base domain.
    /// </summary>
    /// <param name="url">The target URL to check.</param>
    /// <param name="baseDomain">The base domain for comparison.</param>
    /// <returns>True if the URL should be followed, false otherwise.</returns>
    bool ShouldFollowUrl(string url, string baseDomain);

    /// <summary>
    /// Checks if two URLs belong to the same domain.
    /// </summary>
    /// <param name="url1">The first URL to compare.</param>
    /// <param name="url2">The second URL to compare.</param>
    /// <returns>True if both URLs belong to the same domain, false otherwise.</returns>
    bool IsSameDomain(string url1, string url2);
}
