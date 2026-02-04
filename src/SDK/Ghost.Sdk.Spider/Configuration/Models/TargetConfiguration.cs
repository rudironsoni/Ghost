namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for target URLs and crawling scope.
/// </summary>
public sealed class TargetConfiguration
{
    /// <summary>
    /// Gets or sets the starting URLs for the spider.
    /// </summary>
    public List<string> StartUrls { get; set; } = new();

    /// <summary>
    /// Gets or sets URL patterns to include (regex or glob).
    /// </summary>
    public List<string> AllowedPatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets URL patterns to exclude (regex or glob).
    /// </summary>
    public List<string> DeniedPatterns { get; set; } = new();

    /// <summary>
    /// Gets or sets allowed domains. Empty list means no domain restriction.
    /// </summary>
    public List<string> AllowedDomains { get; set; } = new();

    /// <summary>
    /// Gets or sets the maximum depth for crawling. 0 means only start URLs.
    /// </summary>
    public int MaxDepth { get; set; } = 0;

    /// <summary>
    /// Gets or sets whether to follow redirects.
    /// </summary>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to respect robots.txt.
    /// </summary>
    public bool RespectRobotsTxt { get; set; } = true;

    /// <summary>
    /// Gets or sets the user agent string.
    /// </summary>
    public string UserAgent { get; set; } = "Ghost.Sdk.Spider/1.0";

    /// <summary>
    /// Gets or sets custom HTTP headers to include with requests.
    /// </summary>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets authentication configuration.
    /// </summary>
    public AuthenticationConfiguration? Authentication { get; set; }
}

/// <summary>
/// Configuration for authentication.
/// </summary>
public sealed class AuthenticationConfiguration
{
    /// <summary>
    /// Gets or sets the authentication type (Basic, Bearer, Cookie, OAuth2).
    /// </summary>
    public string Type { get; set; } = "Basic";

    /// <summary>
    /// Gets or sets the username for Basic authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for Basic authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the bearer token for Bearer authentication.
    /// </summary>
    public string? Token { get; set; }

    /// <summary>
    /// Gets or sets cookies for Cookie authentication.
    /// </summary>
    public Dictionary<string, string> Cookies { get; set; } = new();

    /// <summary>
    /// Gets or sets OAuth2 configuration.
    /// </summary>
    public OAuth2Configuration? OAuth2 { get; set; }
}

/// <summary>
/// Configuration for OAuth2 authentication.
/// </summary>
public sealed class OAuth2Configuration
{
    /// <summary>
    /// Gets or sets the OAuth2 token endpoint.
    /// </summary>
    public string TokenUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client ID.
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client secret.
    /// </summary>
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the OAuth2 scopes.
    /// </summary>
    public List<string> Scopes { get; set; } = new();
}
