using System.Collections.Generic;

namespace Ghost.ProxyConfiguration;

/// <summary>
/// Configuration for a single proxy source.
/// Defines how to obtain proxies from a specific provider (static hosts, API, etc.)
/// </summary>
public class ProxySourceConfig
{
    /// <summary>
    /// Type of proxy source. Examples: "Static", "Api", "Residential", "DataCenter"
    /// Determines how the proxy provider fetches and rotates proxies.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Whether this proxy source is enabled.
    /// Disabled sources are skipped during proxy fetching.
    /// Defaults to true.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Optional username for proxy authentication.
    /// Used when the proxy provider requires basic authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Optional password for proxy authentication.
    /// Used in conjunction with Username for authenticated proxy access.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// List of proxy hosts for static proxy sources.
    /// Supports formats: "host:port", "host", "scheme://host:port", "scheme://user:pass@host:port"
    /// Used primarily for direct static proxy configuration.
    /// </summary>
    public List<string> Hosts { get; set; } = new();

    /// <summary>
    /// URL endpoint for API-based proxy sources.
    /// Used when proxies are fetched from a remote API provider.
    /// Examples: "https://api.proxy-provider.com/get-proxies"
    /// </summary>
    public string? Url { get; set; }
}
