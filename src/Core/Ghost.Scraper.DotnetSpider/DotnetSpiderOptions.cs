using Ghost.Models;

namespace Ghost.Scraper.DotnetSpider;

/// <summary>
/// Strategy for DotnetSpider download fallback behavior.
/// </summary>
public enum DownloadFallbackStrategy
{
    /// <summary>
    /// Fall back to Ghost browser sessions when HTTP download fails.
    /// </summary>
    GhostSessionFallback,

    /// <summary>
    /// Only use HTTP downloads, never attempt Ghost browser fallback.
    /// </summary>
    HttpOnly
}

/// <summary>
/// Configuration options for DotnetSpider scraping framework integration.
/// </summary>
public sealed class DotnetSpiderOptions
{
    /// <summary>
    /// When true, enables DotnetSpider scraping capabilities.
    /// When false, scraping requests are disabled and return ServiceUnavailable.
    /// Default is true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Target country for scraping operations.
    /// Default is US.
    /// </summary>
    public CountryCode Country { get; set; } = CountryCode.US;

    /// <summary>
    /// Minimum delay between requests in milliseconds.
    /// Default is 500ms.
    /// </summary>
    public int MinDelayMs { get; set; } = 500;

    /// <summary>
    /// Maximum delay between requests in milliseconds.
    /// Default is 1500ms.
    /// </summary>
    public int MaxDelayMs { get; set; } = 1500;

    /// <summary>
    /// Strategy for download fallback behavior.
    /// Default is GhostSessionFallback for better resilience.
    /// </summary>
    public DownloadFallbackStrategy FallbackStrategy { get; set; } = DownloadFallbackStrategy.GhostSessionFallback;

    /// <summary>
    /// When true, enables fallback to Ghost browser sessions when HTTP download fails.
    /// This provides better handling of JavaScript-heavy websites and anti-bot detection.
    /// Default is true.
    /// </summary>
    public bool EnableFallback { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts for failed requests.
    /// Default is 3 attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Whether to enable exponential backoff with jitter for retries.
    /// Default is true to prevent thundering herd effect.
    /// </summary>
    public bool EnableRetryWithJitter { get; set; } = true;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff.
    /// Default is 1000ms (1 second).
    /// </summary>
    public int RetryBaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Maximum delay in milliseconds for exponential backoff.
    /// Default is 30000ms (30 seconds).
    /// </summary>
    public int RetryMaxDelayMs { get; set; } = 30000;

    /// <summary>
    /// Request timeout in milliseconds.
    /// Default is 30000ms (30 seconds).
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Whether to enable debug mode which saves HTTP responses to files.
    /// Default is false for production use.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Whether to enable structured error reporting in API responses.
    /// Default is true to provide better error information.
    /// </summary>
    public bool EnableStructuredErrors { get; set; } = true;

    /// <summary>
    /// User agent string to use for HTTP requests.
    /// Default is a standard Chrome user agent.
    /// </summary>
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    /// <summary>
    /// Whether to verify SSL certificates for HTTPS requests.
    /// Default is true for security.
    /// </summary>
    public bool VerifySslCertificate { get; set; } = true;
}
