namespace Ghost.Plugin.X;

/// <summary>
/// Configuration options for the X (Twitter) platform.
/// </summary>
public class XOptions
{
    /// <summary>
    /// Gets or sets the base URL for X.
    /// </summary>
    public string BaseUrl { get; set; } = "https://x.com";

    /// <summary>
    /// Gets or sets the page load timeout in seconds.
    /// </summary>
    public int PageLoadTimeout { get; set; } = 30;

    /// <summary>
    /// Gets or sets the scraping strategy.
    /// </summary>
    public ScrapingStrategy ScrapingStrategy { get; set; } = ScrapingStrategy.Resilient;

    /// <summary>
    /// Gets or sets the timezone ID for the browser session.
    /// </summary>
    public string TimezoneId { get; set; } = "America/New_York";

    /// <summary>
    /// Gets or sets the locale for the browser session.
    /// </summary>
    public string Locale { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets a value indicating whether proxy is enabled.
    /// </summary>
    public bool ProxyEnabled { get; set; }

    /// <summary>
    /// Gets or sets the path to the storage state file for cookie persistence.
    /// </summary>
    public string? StorageStatePath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to warm up the session before operations.
    /// </summary>
    public bool WarmUpEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the country code for regional settings.
    /// </summary>
    public string Country { get; set; } = "US";

    /// <summary>
    /// Gets the maximum tweet length (280 characters).
    /// </summary>
    public int MaxTweetLength => 280;

    /// <summary>
    /// Gets the maximum number of media attachments per tweet (4 for images).
    /// </summary>
    public int MaxMediaAttachments => 4;

    /// <summary>
    /// Gets the maximum number of video attachments per tweet (1).
    /// </summary>
    public int MaxVideoAttachments => 1;

    /// <summary>
    /// Gets or sets the maximum file size for images in MB.
    /// </summary>
    public int MaxImageSizeMB { get; set; } = 5;

    /// <summary>
    /// Gets or sets the maximum file size for videos in MB.
    /// </summary>
    public int MaxVideoSizeMB { get; set; } = 512;

    /// <summary>
    /// Gets or sets the delay between tweets in a thread (in milliseconds).
    /// </summary>
    public int ThreadDelayMs { get; set; } = 2000;

    /// <summary>
    /// Gets or sets the maximum number of retries for failed operations.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retries (in milliseconds).
    /// </summary>
    public int RetryDelayMs { get; set; } = 5000;

    /// <summary>
    /// Gets or sets the supported image file extensions.
    /// </summary>
    public IReadOnlyList<string> SupportedImageFormats { get; set; } = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };

    /// <summary>
    /// Gets or sets the supported video file extensions.
    /// </summary>
    public IReadOnlyList<string> SupportedVideoFormats { get; set; } = new[] { ".mp4", ".mov", ".webm" };

    /// <summary>
    /// Gets the page options for browser sessions.
    /// </summary>
    public Ghost.PageOptions GetPageOptions()
    {
        return new Ghost.PageOptions
        {
            TimezoneId = TimezoneId,
            Locale = Locale
        };
    }
}

/// <summary>
/// Scraping strategy options.
/// </summary>
public enum ScrapingStrategy
{
    /// <summary>
    /// Fast scraping with minimal delays.
    /// </summary>
    Fast,

    /// <summary>
    /// Resilient scraping with retries and delays.
    /// </summary>
    Resilient,

    /// <summary>
    /// Stealth scraping with human-like behavior.
    /// </summary>
    Stealth
}
