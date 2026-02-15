namespace Ghost.Sdk.Spider.Statistics;

/// <summary>
/// Statistics about crawl depth distribution.
/// </summary>
public class DepthStatistics
{
    /// <summary>
    /// Gets or sets the maximum depth reached during crawling.
    /// </summary>
    public int MaxDepth { get; set; }

    /// <summary>
    /// Gets or sets the average depth of all tracked URLs.
    /// </summary>
    public double AverageDepth { get; set; }

    /// <summary>
    /// Gets or sets the total number of URLs tracked.
    /// </summary>
    public int TotalUrls { get; set; }

    /// <summary>
    /// Gets or sets the distribution of URLs by depth level.
    /// </summary>
    /// <remarks>
    /// The key is the depth level, and the value is the count of URLs at that depth.
    /// Example: { 0: 1, 1: 5, 2: 20 } means 1 URL at depth 0, 5 at depth 1, and 20 at depth 2.
    /// </remarks>
    public Dictionary<int, int> Distribution { get; set; } = new();
}
