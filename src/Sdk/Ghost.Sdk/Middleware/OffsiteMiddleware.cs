namespace Ghost.Sdk.Middleware;

/// <summary>
/// Middleware that prevents crawling of external domains (offsite links).
/// </summary>
public class OffsiteMiddleware : IOffsiteMiddleware
{
    private readonly OffsiteOptions _options;
    private readonly HashSet<string> _allowedDomains;
    private readonly HashSet<string> _denyDomains;

    /// <summary>
    /// Initializes a new instance of the <see cref="OffsiteMiddleware"/> class.
    /// </summary>
    /// <param name="options">The offsite middleware options.</param>
    public OffsiteMiddleware(OffsiteOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;
        _allowedDomains = new HashSet<string>(options.AllowedDomains, StringComparer.OrdinalIgnoreCase);
        _denyDomains = new HashSet<string>(options.DenyDomains, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool ShouldFollowUrl(string url, string baseDomain)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(baseDomain);

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
            return false;

        string targetDomain = uri.Host;

        // Check deny list first (takes precedence)
        if (_denyDomains.Contains(targetDomain))
            return false;

        // Check allowed domains list
        if (_allowedDomains.Contains(targetDomain))
            return true;

        // Check same domain
        if (_options.AllowSubdomains)
        {
            return IsSubdomain(targetDomain, baseDomain);
        }

        return string.Equals(targetDomain, baseDomain, StringComparison.OrdinalIgnoreCase);
    }

    /// <inheritdoc/>
    public bool IsSameDomain(string url1, string url2)
    {
        ArgumentNullException.ThrowIfNull(url1);
        ArgumentNullException.ThrowIfNull(url2);

        if (!Uri.TryCreate(url1, UriKind.Absolute, out Uri? uri1))
            return false;

        if (!Uri.TryCreate(url2, UriKind.Absolute, out Uri? uri2))
            return false;

        return string.Equals(uri1.Host, uri2.Host, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSubdomain(string subdomain, string domain)
    {
        return subdomain.EndsWith($".{domain}", StringComparison.OrdinalIgnoreCase)
            || string.Equals(subdomain, domain, StringComparison.OrdinalIgnoreCase);
    }
}
