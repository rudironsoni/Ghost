namespace Ghost.Sdk.Extraction;

/// <summary>
/// Configuration options for link extraction.
/// </summary>
public sealed class LinkExtractorOptions
{
    /// <summary>
    /// Gets or sets the allowed file extensions (e.g., ".html", ".htm", "").
    /// Empty string allows URLs without extensions.
    /// If null or empty, all extensions are allowed.
    /// </summary>
    public IReadOnlyCollection<string>? AllowedExtensions { get; set; }

    /// <summary>
    /// Gets or sets the denied file extensions (e.g., ".jpg", ".png", ".pdf").
    /// These will be excluded from extraction regardless of other filters.
    /// </summary>
    public IReadOnlyCollection<string>? DenyExtensions { get; set; }

    /// <summary>
    /// Gets or sets the allowed domains. If specified, only URLs from these domains will be extracted.
    /// Supports exact matches (e.g., "example.com").
    /// </summary>
    public IReadOnlyCollection<string>? AllowedDomains { get; set; }

    /// <summary>
    /// Gets or sets the XPath expressions to restrict link extraction to specific areas.
    /// If specified, only links within matching elements will be extracted.
    /// </summary>
    public IReadOnlyCollection<string>? RestrictXpaths { get; set; }

    /// <summary>
    /// Gets or sets the CSS selectors to restrict link extraction to specific areas.
    /// If specified, only links within matching elements will be extracted.
    /// </summary>
    public IReadOnlyCollection<string>? RestrictCssSelectors { get; set; }

    /// <summary>
    /// Gets or sets whether to strip URL fragments (e.g., #section).
    /// Default is true.
    /// </summary>
    public bool StripFragments { get; set; } = true;

    /// <summary>
    /// Gets or sets whether to return only unique URLs.
    /// Default is true.
    /// </summary>
    public bool UniqueOnly { get; set; } = true;
}
