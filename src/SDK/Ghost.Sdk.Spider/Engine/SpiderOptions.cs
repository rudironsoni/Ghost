namespace Ghost.Sdk.Spider.Engine;

/// <summary>
/// Configuration options for spider execution.
/// </summary>
public class SpiderOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent requests.
    /// </summary>
    /// <value>The concurrency limit. Defaults to 1.</value>
    public int MaxConcurrency { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum number of requests to process.
    /// </summary>
    /// <value>The request limit, or null for unlimited. Defaults to null.</value>
    public int? MaxRequests { get; set; }

    /// <summary>
    /// Gets or sets the delay between requests.
    /// </summary>
    /// <value>The delay duration. Defaults to 1 second.</value>
    public TimeSpan RequestDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets the request timeout.
    /// </summary>
    /// <value>The timeout duration. Defaults to 30 seconds.</value>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum depth for crawling.
    /// </summary>
    /// <value>The maximum depth, or null for unlimited. Defaults to null.</value>
    public int? MaxDepth { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to respect robots.txt.
    /// </summary>
    /// <value><c>true</c> to obey robots.txt; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>
    /// Gets or sets the user agent string.
    /// </summary>
    /// <value>The user agent. Defaults to a standard browser user agent.</value>
    public string UserAgent { get; set; } = "Ghost.Sdk.Spider/1.0";

    /// <summary>
    /// Gets or sets allowed domains for crawling.
    /// </summary>
    /// <value>List of allowed domain patterns, or empty for all domains.</value>
    public List<string> AllowedDomains { get; set; } = new();

    /// <summary>
    /// Gets or sets URL patterns to exclude.
    /// </summary>
    /// <value>List of regex patterns for URLs to skip.</value>
    public List<string> ExcludePatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to enable auto-throttling.
    /// </summary>
    /// <value><c>true</c> to automatically adjust request rate; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    public bool EnableAutoThrottle { get; set; } = false;

    /// <summary>
    /// Gets or sets custom metadata for the spider.
    /// </summary>
    /// <value>Dictionary of custom key-value pairs.</value>
    public Dictionary<string, object> Metadata { get; set; } = new();
}
