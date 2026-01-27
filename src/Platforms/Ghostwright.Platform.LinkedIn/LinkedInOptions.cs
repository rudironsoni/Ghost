namespace Ghostwright.Platform.LinkedIn;

/// <summary>
/// Options for the LinkedIn browser automation.
/// </summary>
public sealed class LinkedInOptions
{
    public string BaseUrl { get; set; } = "https://www.linkedin.com";
    public TimeSpan PageLoadTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public JobScrapingStrategy ScrapingStrategy { get; set; } = JobScrapingStrategy.GuestApi;
}

public enum JobScrapingStrategy
{
    GuestApi,
    BrowserPage,
    Hybrid
}
