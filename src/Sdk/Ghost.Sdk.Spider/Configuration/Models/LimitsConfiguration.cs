namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for resource limits and constraints.
/// </summary>
public sealed class LimitsConfiguration
{
    /// <summary>
    /// Gets or sets the maximum number of pages to crawl. 0 means no limit.
    /// </summary>
    public int MaxPages { get; set; }

    /// <summary>
    /// Gets or sets the maximum crawl duration (seconds). 0 means no limit.
    /// </summary>
    public int MaxDurationSeconds { get; set; }

    /// <summary>
    /// Gets or sets the maximum file size to download (bytes). 0 means no limit.
    /// </summary>
    public long MaxFileSizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum total download size (bytes). 0 means no limit.
    /// </summary>
    public long MaxTotalDownloadBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum memory usage (bytes). 0 means no limit.
    /// </summary>
    public long MaxMemoryBytes { get; set; }

    /// <summary>
    /// Gets or sets the maximum queue size for URLs.
    /// </summary>
    public int MaxQueueSize { get; set; } = 10000;

    /// <summary>
    /// Gets or sets the maximum number of retries per URL.
    /// </summary>
    public int MaxRetriesPerUrl { get; set; } = 3;

    /// <summary>
    /// Gets or sets the request timeout (seconds).
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the page load timeout (seconds).
    /// </summary>
    public int PageLoadTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets the maximum number of concurrent browser contexts.
    /// </summary>
    public int MaxBrowserContexts { get; set; } = 5;

    /// <summary>
    /// Gets or sets allowed content types (MIME types). Empty list means all types allowed.
    /// </summary>
    public List<string> AllowedContentTypes { get; set; } = new();

    /// <summary>
    /// Gets or sets blocked content types (MIME types).
    /// </summary>
    public List<string> BlockedContentTypes { get; set; } = new()
    {
        "image/*",
        "video/*",
        "audio/*",
        "font/*"
    };

    /// <summary>
    /// Gets or sets resource blocking configuration.
    /// </summary>
    public ResourceBlockingConfiguration ResourceBlocking { get; set; } = new();
}

/// <summary>
/// Configuration for blocking resources during crawling.
/// </summary>
public sealed class ResourceBlockingConfiguration
{
    /// <summary>
    /// Gets or sets whether to block images.
    /// </summary>
    public bool BlockImages { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to block stylesheets.
    /// </summary>
    public bool BlockStylesheets { get; set; }

    /// <summary>
    /// Gets or sets whether to block fonts.
    /// </summary>
    public bool BlockFonts { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to block media (video/audio).
    /// </summary>
    public bool BlockMedia { get; set; } = true;

    /// <summary>
    /// Gets or sets custom URL patterns to block.
    /// </summary>
    public List<string> BlockedUrlPatterns { get; set; } = new();
}
