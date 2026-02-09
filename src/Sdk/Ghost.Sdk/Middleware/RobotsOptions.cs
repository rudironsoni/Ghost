namespace Ghost.Sdk.Middleware;

/// <summary>
/// Configuration options for robots.txt middleware.
/// </summary>
public class RobotsOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to allow fetching when robots.txt cannot be retrieved or parsed.
    /// </summary>
    public bool AllowOnError { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for fetching robots.txt files.
    /// </summary>
    public TimeSpan? Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value indicating whether to respect crawl-delay directives.
    /// </summary>
    public bool RespectCrawlDelay { get; set; } = true;
}
