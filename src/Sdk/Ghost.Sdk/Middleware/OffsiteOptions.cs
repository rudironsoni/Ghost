namespace Ghost.Sdk.Middleware;

/// <summary>
/// Configuration options for offsite middleware.
/// </summary>
public class OffsiteOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether subdomains should be allowed.
    /// </summary>
    public bool AllowSubdomains { get; set; } = true;

    /// <summary>
    /// Gets or sets the list of allowed domains.
    /// </summary>
    public List<string> AllowedDomains { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of denied domains.
    /// </summary>
    public List<string> DenyDomains { get; set; } = [];
}
