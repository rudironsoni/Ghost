using Ghost.Models;

namespace Ghost.Plugin.Glassdoor;

/// <summary>
/// Strategy for attempting job search methods.
/// </summary>
public enum JobSearchStrategy
{
    /// <summary>
    /// Try HTTP API first, fall back to browser if no results.
    /// </summary>
    HttpFirst,

    /// <summary>
    /// Try browser first, fall back to HTTP API if browser fails.
    /// </summary>
    BrowserFirst,

    /// <summary>
    /// Only use HTTP API, never attempt browser.
    /// </summary>
    HttpOnly,

    /// <summary>
    /// Only use browser, never attempt HTTP API.
    /// </summary>
    BrowserOnly
}

public sealed class GlassdoorOptions
{
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Base URL for Glassdoor website
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.glassdoor.com";

    /// <summary>
    /// When true, the Glassdoor HTTP client will attempt to use the configured proxy provider.
    /// When false, the client will use a direct connection.
    /// Defaults to FALSE to avoid proxy authentication issues with SOCKS5 proxies.
    /// </summary>
    public bool ProxyEnabled { get; set; }

    public CountryCode Country { get; set; } = CountryCode.US;

    /// <summary>
    /// Minimum delay between requests in milliseconds.
    /// </summary>
    public int DelayMinMs { get; set; } = 500;

    /// <summary>
    /// Strategy for attempting job search methods.
    /// Default is BrowserFirst for better reliability.
    /// </summary>
    public JobSearchStrategy Strategy { get; set; } = JobSearchStrategy.BrowserFirst;

    /// <summary>
    /// Maximum number of retry attempts for failed requests.
    /// Default is 4 attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 4;

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
    /// Whether to enable debug mode which saves HTML/JSON responses to files.
    /// Default is false for production use.
    /// </summary>
    public bool DebugMode { get; set; }

    /// <summary>
    /// Request timeout in milliseconds.
    /// Default is 30000ms (30 seconds).
    /// </summary>
    public int RequestTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Whether to enable structured error reporting in API responses.
    /// Default is true to provide better error information.
    /// </summary>
    public bool EnableStructuredErrors { get; set; } = true;

    /// <summary>
    /// When true, enables test mode which skips production-grade delays
    /// (Cloudflare waits, human-like scrolling delays, etc.) for faster test execution.
    /// Default is false for production use.
    /// </summary>
    public bool TestMode { get; set; }
}
