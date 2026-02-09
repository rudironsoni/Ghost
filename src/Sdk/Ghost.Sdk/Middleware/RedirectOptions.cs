namespace Ghost.Sdk.Middleware;

/// <summary>
/// Configuration options for HTTP redirect handling.
/// </summary>
/// <remarks>
/// These options control how HTTP redirects (301, 302, 303, 307, 308) are followed,
/// including the maximum number of redirects and whether cross-scheme redirects are allowed.
/// </remarks>
public class RedirectOptions
{
    /// <summary>
    /// Gets or sets the maximum number of redirects to follow before throwing an exception.
    /// </summary>
    /// <value>Default is 10 redirects.</value>
    /// <remarks>
    /// This prevents infinite redirect loops and limits the overhead of following
    /// long redirect chains.
    /// </remarks>
    public int MaxRedirects { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to allow redirects that change the URL scheme (e.g., http to https).
    /// </summary>
    /// <value>Default is false (cross-scheme redirects are not allowed).</value>
    /// <remarks>
    /// Enabling this allows redirects from HTTP to HTTPS or vice versa. Disabling provides
    /// additional security by preventing potential protocol downgrade attacks.
    /// </remarks>
    public bool AllowCrossScheme { get; set; }
}
