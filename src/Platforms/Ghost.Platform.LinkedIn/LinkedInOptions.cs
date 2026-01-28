namespace Ghost.Platform.LinkedIn;

/// <summary>
/// Options for the LinkedIn browser automation.
/// </summary>
public sealed class LinkedInOptions
{
    public string BaseUrl { get; set; } = "https://www.linkedin.com";
    public TimeSpan PageLoadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public JobScrapingStrategy ScrapingStrategy { get; set; } = JobScrapingStrategy.GuestApi;

    /// <summary>
    /// Explicitly set the Timezone (e.g. "Europe/Madrid") to match the proxy.
    /// If null, system default is used (risky for stealth).
    /// </summary>
    public string? TimezoneId { get; set; }

    /// <summary>
    /// Explicitly set the Locale (e.g. "es-ES") to match the proxy.
    /// </summary>
    public string? Locale { get; set; }
}

public enum JobScrapingStrategy
{
    GuestApi,
    BrowserPage,
    Hybrid
}
