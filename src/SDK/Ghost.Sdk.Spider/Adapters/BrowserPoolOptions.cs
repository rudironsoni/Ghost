namespace Ghost.Sdk.Spider.Adapters;

/// <summary>
/// Configuration options for browser pool management.
/// </summary>
/// <remarks>
/// These options control how browser instances are created, managed, and recycled
/// within the browser pool to optimize performance and resource usage.
/// </remarks>
public class BrowserPoolOptions
{
    /// <summary>
    /// Gets or sets the maximum number of concurrent browser instances in the pool.
    /// </summary>
    /// <value>The maximum pool size. Defaults to 5.</value>
    /// <remarks>
    /// This limit prevents excessive resource consumption. Each browser instance
    /// consumes significant memory (typically 100-200 MB). Consider your available
    /// resources when setting this value.
    /// </remarks>
    public int MaxPoolSize { get; set; } = 5;

    /// <summary>
    /// Gets or sets the minimum number of browser instances to keep ready.
    /// </summary>
    /// <value>The minimum pool size. Defaults to 1.</value>
    /// <remarks>
    /// Maintaining a minimum number of idle browsers reduces latency for incoming
    /// requests by avoiding the overhead of browser startup.
    /// </remarks>
    public int MinPoolSize { get; set; } = 1;

    /// <summary>
    /// Gets or sets the maximum lifetime of a browser instance before recycling.
    /// </summary>
    /// <value>The maximum browser lifetime. Defaults to 30 minutes.</value>
    /// <remarks>
    /// Recycling browsers periodically helps prevent memory leaks and ensures
    /// optimal performance. Browsers exceeding this lifetime are disposed and
    /// replaced with fresh instances.
    /// </remarks>
    public TimeSpan MaxBrowserLifetime { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the maximum idle time before a browser is disposed.
    /// </summary>
    /// <value>The maximum idle duration. Defaults to 5 minutes.</value>
    /// <remarks>
    /// Idle browsers that exceed this duration are disposed to free resources,
    /// but the pool will maintain at least <see cref="MinPoolSize"/> instances.
    /// </remarks>
    public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the timeout for acquiring a browser from the pool.
    /// </summary>
    /// <value>The acquisition timeout. Defaults to 30 seconds.</value>
    /// <remarks>
    /// If no browser becomes available within this timeout, the acquisition
    /// operation fails. Increase this value if you expect high contention.
    /// </remarks>
    public TimeSpan AcquisitionTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to launch browsers in headless mode.
    /// </summary>
    /// <value><c>true</c> for headless mode; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    /// <remarks>
    /// Headless mode runs browsers without a visible UI, which reduces resource
    /// usage and is suitable for server environments. Set to false for debugging.
    /// </remarks>
    public bool Headless { get; set; } = true;

    /// <summary>
    /// Gets or sets the browser type to use.
    /// </summary>
    /// <value>The browser type name. Defaults to "chromium".</value>
    /// <remarks>
    /// Supported values: "chromium", "firefox", "webkit".
    /// Chromium generally provides the best compatibility with modern web applications.
    /// </remarks>
    public string BrowserType { get; set; } = "chromium";

    /// <summary>
    /// Gets or sets additional browser launch arguments.
    /// </summary>
    /// <value>A list of command-line arguments to pass to the browser.</value>
    /// <remarks>
    /// Common arguments include:
    /// <list type="bullet">
    /// <item>--disable-gpu: Disable GPU hardware acceleration</item>
    /// <item>--no-sandbox: Disable sandbox (use with caution in trusted environments)</item>
    /// <item>--disable-dev-shm-usage: Prevent /dev/shm resource issues in containers</item>
    /// <item>--window-size=1920,1080: Set viewport size</item>
    /// </list>
    /// </remarks>
    public List<string> BrowserArgs { get; set; } = new();

    /// <summary>
    /// Gets or sets the default viewport width for browser instances.
    /// </summary>
    /// <value>The viewport width in pixels. Defaults to 1920.</value>
    public int ViewportWidth { get; set; } = 1920;

    /// <summary>
    /// Gets or sets the default viewport height for browser instances.
    /// </summary>
    /// <value>The viewport height in pixels. Defaults to 1080.</value>
    public int ViewportHeight { get; set; } = 1080;

    /// <summary>
    /// Gets or sets a value indicating whether to enable browser cache.
    /// </summary>
    /// <value><c>true</c> to enable cache; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    /// <remarks>
    /// Disabling cache ensures fresh content on each request, which is typically
    /// desired for web scraping. Enable caching if you need to test caching behavior
    /// or reduce bandwidth usage.
    /// </remarks>
    public bool EnableCache { get; set; } = false;

    /// <summary>
    /// Gets or sets a value indicating whether to enable JavaScript execution.
    /// </summary>
    /// <value><c>true</c> to enable JavaScript; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    /// <remarks>
    /// Disabling JavaScript can improve performance and security for static content,
    /// but is required for most modern web applications.
    /// </remarks>
    public bool EnableJavaScript { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to load images.
    /// </summary>
    /// <value><c>true</c> to load images; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    /// <remarks>
    /// Disabling image loading can significantly reduce bandwidth and improve
    /// performance when images are not needed for content extraction.
    /// </remarks>
    public bool LoadImages { get; set; } = true;

    /// <summary>
    /// Gets or sets the locale to use for browser instances.
    /// </summary>
    /// <value>The locale code (e.g., "en-US", "de-DE"). Defaults to "en-US".</value>
    public string Locale { get; set; } = "en-US";

    /// <summary>
    /// Gets or sets the timezone to use for browser instances.
    /// </summary>
    /// <value>The timezone identifier (e.g., "America/New_York"). Defaults to "UTC".</value>
    public string Timezone { get; set; } = "UTC";

    /// <summary>
    /// Validates the browser pool options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    public void Validate()
    {
        if (MaxPoolSize <= 0)
        {
            throw new ArgumentException("MaxPoolSize must be greater than zero.", nameof(MaxPoolSize));
        }

        if (MinPoolSize < 0)
        {
            throw new ArgumentException("MinPoolSize cannot be negative.", nameof(MinPoolSize));
        }

        if (MinPoolSize > MaxPoolSize)
        {
            throw new ArgumentException("MinPoolSize cannot exceed MaxPoolSize.", nameof(MinPoolSize));
        }

        if (MaxBrowserLifetime <= TimeSpan.Zero)
        {
            throw new ArgumentException("MaxBrowserLifetime must be greater than zero.", nameof(MaxBrowserLifetime));
        }

        if (MaxIdleTime <= TimeSpan.Zero)
        {
            throw new ArgumentException("MaxIdleTime must be greater than zero.", nameof(MaxIdleTime));
        }

        if (AcquisitionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("AcquisitionTimeout must be greater than zero.", nameof(AcquisitionTimeout));
        }

        if (string.IsNullOrWhiteSpace(BrowserType))
        {
            throw new ArgumentException("BrowserType cannot be null or whitespace.", nameof(BrowserType));
        }

        var supportedBrowsers = new[] { "chromium", "firefox", "webkit" };
        if (!supportedBrowsers.Contains(BrowserType.ToLowerInvariant()))
        {
            throw new ArgumentException(
                $"BrowserType '{BrowserType}' is not supported. Use one of: {string.Join(", ", supportedBrowsers)}",
                nameof(BrowserType));
        }

        if (ViewportWidth <= 0)
        {
            throw new ArgumentException("ViewportWidth must be greater than zero.", nameof(ViewportWidth));
        }

        if (ViewportHeight <= 0)
        {
            throw new ArgumentException("ViewportHeight must be greater than zero.", nameof(ViewportHeight));
        }
    }
}
