using Ghost.Models;

namespace Ghost.Platform.Indeed;

public class IndeedOptions
{
    public bool Enabled { get; set; } = true;
    public CountryCode Country { get; set; } = CountryCode.US;
    public int DelayMinMs { get; set; } = 500;
    public int DelayMaxMs { get; set; } = 1500;
    public int MaxRetries { get; set; } = 3;
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// API endpoint for Indeed GraphQL API
    /// </summary>
    public string ApiEndpoint { get; set; } = "https://apis.indeed.com/graphql";

    /// <summary>
    /// Base URL for Indeed website (for test mocking support)
    /// </summary>
    public string BaseUrl { get; set; } = "https://www.indeed.com";
}
