namespace Ghost.Sdk.Spider.Adapters.Contracts;

/// <summary>
/// Represents the result of a content extraction operation.
/// </summary>
/// <remarks>
/// This class encapsulates the extracted content along with metadata about the extraction
/// process. It provides a unified model for content regardless of the source type
/// (HTML, JSON, XML, binary, etc.).
/// </remarks>
public class ContentResult
{
    /// <summary>
    /// Gets or sets the extracted content.
    /// </summary>
    /// <value>
    /// The raw content as extracted from the source. This may be HTML, JSON, XML,
    /// plain text, or other formats depending on the adapter used.
    /// </value>
    /// <remarks>
    /// The content format is typically indicated by the <see cref="ContentType"/> property.
    /// Consumers should check the content type before processing the content string.
    /// </remarks>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the content type of the extracted content.
    /// </summary>
    /// <value>The type of content contained in the <see cref="Content"/> property.</value>
    public ContentType ContentType { get; set; }

    /// <summary>
    /// Gets or sets the MIME type of the content.
    /// </summary>
    /// <value>
    /// The MIME type as reported by the source (e.g., "text/html", "application/json").
    /// May be null if the MIME type is unknown or not applicable.
    /// </value>
    public string? MimeType { get; set; }

    /// <summary>
    /// Gets or sets the character encoding of the content.
    /// </summary>
    /// <value>
    /// The encoding used for the content (e.g., "utf-8", "iso-8859-1").
    /// Defaults to "utf-8" if not specified.
    /// </value>
    public string Encoding { get; set; } = "utf-8";

    /// <summary>
    /// Gets or sets the size of the content in bytes.
    /// </summary>
    /// <value>The size of the content, or null if size information is not available.</value>
    public long? ContentLength { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the content was extracted.
    /// </summary>
    /// <value>The UTC timestamp of the extraction operation.</value>
    public DateTimeOffset ExtractedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets a value indicating whether the content extraction was successful.
    /// </summary>
    /// <value>
    /// <c>true</c> if content was successfully extracted; otherwise, <c>false</c>.
    /// </value>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets error information if the extraction failed.
    /// </summary>
    /// <value>
    /// A description of the error that occurred during extraction, or null if successful.
    /// </value>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets additional metadata associated with the content.
    /// </summary>
    /// <value>
    /// A dictionary of metadata key-value pairs. This may include custom headers,
    /// extraction parameters, performance metrics, or adapter-specific information.
    /// </value>
    /// <remarks>
    /// Common metadata keys might include:
    /// <list type="bullet">
    /// <item>"AdapterName": The adapter that extracted the content</item>
    /// <item>"ExtractionTimeMs": Time taken to extract content in milliseconds</item>
    /// <item>"RetryCount": Number of retry attempts made</item>
    /// <item>"CacheHit": Whether content was served from cache</item>
    /// </list>
    /// </remarks>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentResult"/> class.
    /// </summary>
    public ContentResult()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ContentResult"/> class with the specified content.
    /// </summary>
    /// <param name="content">The extracted content.</param>
    /// <param name="contentType">The type of the content.</param>
    public ContentResult(string content, ContentType contentType)
    {
        Content = content;
        ContentType = contentType;
        Success = true;
    }

    /// <summary>
    /// Creates a successful content result.
    /// </summary>
    /// <param name="content">The extracted content.</param>
    /// <param name="contentType">The type of the content.</param>
    /// <returns>A new <see cref="ContentResult"/> instance marked as successful.</returns>
    public static ContentResult CreateSuccess(string content, ContentType contentType)
    {
        return new ContentResult(content, contentType);
    }

    /// <summary>
    /// Creates a failed content result.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <param name="contentType">The expected content type.</param>
    /// <returns>A new <see cref="ContentResult"/> instance marked as failed.</returns>
    public static ContentResult CreateFailure(string error, ContentType contentType = ContentType.Unknown)
    {
        return new ContentResult
        {
            Success = false,
            Error = error,
            ContentType = contentType
        };
    }
}
