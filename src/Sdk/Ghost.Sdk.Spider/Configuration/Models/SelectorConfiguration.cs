namespace Ghost.Sdk.Spider.Configuration.Models;

/// <summary>
/// Configuration for content selectors.
/// </summary>
public sealed class SelectorConfiguration
{
    /// <summary>
    /// Gets or sets the selector type (CSS, XPath, JsonPath, JMESPath, Regex).
    /// </summary>
    public string Type { get; set; } = "CSS";

    /// <summary>
    /// Gets or sets the selector expression.
    /// </summary>
    public string Expression { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the attribute to extract (for HTML selectors). Null means text content.
    /// </summary>
    public string? Attribute { get; set; }

    /// <summary>
    /// Gets or sets whether to extract all matches (default: false, first match only).
    /// </summary>
    public bool Multiple { get; set; }

    /// <summary>
    /// Gets or sets whether to extract inner HTML instead of text.
    /// </summary>
    public bool ExtractHtml { get; set; }

    /// <summary>
    /// Gets or sets regex pattern to apply after initial extraction.
    /// </summary>
    public string? PostRegex { get; set; }

    /// <summary>
    /// Gets or sets the regex group to extract (default: 0 for entire match).
    /// </summary>
    public int RegexGroup { get; set; }
}
