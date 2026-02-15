namespace Ghost.Sdk.Spider.Core.Extraction;

/// <summary>
/// Represents the context in which entity extraction occurs, including the content and metadata.
/// </summary>
public class ExtractionContext
{
    /// <summary>
    /// Gets or sets the raw content to extract from.
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// Gets or sets the content type (e.g., "text/html", "application/json").
    /// </summary>
    public string ContentType { get; init; } = "text/html";

    /// <summary>
    /// Gets or sets the source URL from which the content was retrieved.
    /// </summary>
    public string? SourceUrl { get; init; }

    /// <summary>
    /// Gets or sets the base URL for resolving relative URLs.
    /// </summary>
    public string? BaseUrl { get; init; }

    /// <summary>
    /// Gets or sets the encoding of the content.
    /// </summary>
    public string Encoding { get; init; } = "UTF-8";

    /// <summary>
    /// Gets or sets additional metadata about the extraction context.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets or sets the timestamp when the extraction started.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets a value indicating whether the content is HTML.
    /// </summary>
    public bool IsHtml => ContentType.Contains("html", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether the content is JSON.
    /// </summary>
    public bool IsJson => ContentType.Contains("json", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets a value indicating whether the content is XML.
    /// </summary>
    public bool IsXml => ContentType.Contains("xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Creates a child context for nested entity extraction.
    /// </summary>
    /// <param name="content">The content for the child context.</param>
    /// <returns>A new <see cref="ExtractionContext"/> instance.</returns>
    public ExtractionContext CreateChildContext(string content)
    {
        return new ExtractionContext
        {
            Content = content,
            ContentType = ContentType,
            SourceUrl = SourceUrl,
            BaseUrl = BaseUrl,
            Encoding = Encoding,
            Metadata = new Dictionary<string, object>(Metadata),
            Timestamp = Timestamp
        };
    }
}
