using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Statistics;

/// <summary>
/// Provides depth tracking for crawled requests.
/// </summary>
/// <remarks>
/// Depth represents the number of link hops from the starting seed URL to the current page.
/// This is essential for controlling crawl scope and preventing infinite loops.
/// </remarks>
public interface IDepthTracker
{
    /// <summary>
    /// Gets the depth of a request.
    /// </summary>
    /// <param name="request">The request to get depth for.</param>
    /// <returns>The depth of the request, or 0 if not tracked.</returns>
    public int GetDepth(Request request);

    /// <summary>
    /// Sets the depth of a request.
    /// </summary>
    /// <param name="request">The request to set depth for.</param>
    /// <param name="depth">The depth value to set.</param>
    public void SetDepth(Request request, int depth);

    /// <summary>
    /// Gets statistics about tracked depths.
    /// </summary>
    /// <returns>Statistics about crawl depth distribution.</returns>
    public DepthStatistics GetStatistics();
}
