namespace Ghost.Platform.Glassdoor.Internal;

internal static class GlassdoorConstants
{
    public static string ApiUrl => "https://www.glassdoor.com/graph";

    // Minimal identifier for the GraphQL operation used by Glassdoor - kept as a template placeholder
    public const string QueryTemplate = "JobSearchResultsQuery";

    // Fallback token used when CSRF cannot be obtained
    public const string FallbackToken = "S...";

    /// <summary>
    /// Comprehensive browser headers for CSRF token retrieval (GET request to Glassdoor homepage)
    /// These headers simulate a real browser request to avoid blocking by anti-bot measures.
    /// </summary>
    public static readonly Dictionary<string, string> CsrfHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8,application/signed-exchange;v=b3;q=0.7",
        ["Accept-Language"] = "en-US,en;q=0.9",
        ["Accept-Encoding"] = "gzip, deflate, br",
        ["Connection"] = "keep-alive",
        ["Upgrade-Insecure-Requests"] = "1",
        ["Sec-Fetch-Dest"] = "document",
        ["Sec-Fetch-Mode"] = "navigate",
        ["Sec-Fetch-Site"] = "none",
        ["Sec-Fetch-User"] = "?1",
        ["Sec-Ch-Ua"] = "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"",
        ["Sec-Ch-Ua-Mobile"] = "?0",
        ["Sec-Ch-Ua-Platform"] = "\"Windows\"",
        ["Cache-Control"] = "max-age=0"
    };

    /// <summary>
    /// Comprehensive browser headers for GraphQL queries (POST request to /graph endpoint)
    /// These headers simulate a real browser request to avoid blocking by anti-bot measures.
    /// The gd-csrf-token header is added dynamically by GlassdoorApiClient.
    /// </summary>
    public static readonly Dictionary<string, string> GraphHeaders = new()
    {
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
        ["Accept"] = "*/*",
        ["Accept-Language"] = "en-US,en;q=0.9",
        ["Accept-Encoding"] = "gzip, deflate, br",
        ["Content-Type"] = "application/json",
        ["Origin"] = "https://www.glassdoor.com",
        ["Referer"] = "https://www.glassdoor.com/",
        ["Connection"] = "keep-alive",
        ["Sec-Fetch-Dest"] = "empty",
        ["Sec-Fetch-Mode"] = "cors",
        ["Sec-Fetch-Site"] = "same-origin",
        ["Sec-Ch-Ua"] = "\"Not_A Brand\";v=\"8\", \"Chromium\";v=\"120\", \"Google Chrome\";v=\"120\"",
        ["Sec-Ch-Ua-Mobile"] = "?0",
        ["Sec-Ch-Ua-Platform"] = "\"Windows\"",
        // Apollo GraphQL client identifiers (these are required by Glassdoor)
        ["apollographql-client-name"] = "Glassdoor-Frontend",
        ["apollographql-client-version"] = "1.0"
    };
}
