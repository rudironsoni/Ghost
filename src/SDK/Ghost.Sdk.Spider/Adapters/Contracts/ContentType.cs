namespace Ghost.Sdk.Spider.Adapters.Contracts;

/// <summary>
/// Defines the types of content that can be extracted by adapters.
/// </summary>
/// <remarks>
/// This enumeration categorizes content by its nature and the techniques required
/// to extract it. Different adapter implementations specialize in handling specific
/// content types.
/// </remarks>
public enum ContentType
{
    /// <summary>
    /// Unknown or unspecified content type.
    /// </summary>
    /// <remarks>
    /// Used when the content type cannot be determined or when auto-detection is needed.
    /// Adapters may attempt to detect the actual content type when this value is specified.
    /// </remarks>
    Unknown = 0,

    /// <summary>
    /// Static HTML content that can be retrieved with a simple HTTP request.
    /// </summary>
    /// <remarks>
    /// This content type is suitable for pages that do not require JavaScript execution
    /// and can be fully parsed from the initial HTTP response. StaticHtml adapters are
    /// typically the fastest and most efficient option.
    /// </remarks>
    StaticHtml = 1,

    /// <summary>
    /// HTML content (alias for StaticHtml).
    /// </summary>
    Html = StaticHtml,

    /// <summary>
    /// JavaScript-rendered content requiring browser execution.
    /// </summary>
    /// <remarks>
    /// This content type is for Single Page Applications (SPAs) and Progressive Web Apps (PWAs)
    /// that require JavaScript execution to render content. JavaScript adapters use headless
    /// browsers to execute scripts and extract the rendered DOM.
    /// </remarks>
    JavaScript = 2,

    /// <summary>
    /// JSON data content.
    /// </summary>
    /// <remarks>
    /// This content type is for REST API responses and JSON documents.
    /// JSON adapters handle parsing, schema validation, and may use JSONPath or JMESPath
    /// for data extraction.
    /// </remarks>
    Json = 3,

    /// <summary>
    /// XML document content.
    /// </summary>
    /// <remarks>
    /// This content type is for XML documents, SOAP services, and RSS/Atom feeds.
    /// XML adapters handle parsing, namespace resolution, and XPath queries.
    /// </remarks>
    Xml = 4,

    /// <summary>
    /// GraphQL API content.
    /// </summary>
    /// <remarks>
    /// This content type is for GraphQL endpoints and schema introspection.
    /// GraphQL adapters handle query construction, variable substitution, and response parsing.
    /// </remarks>
    GraphQL = 5,

    /// <summary>
    /// WebSocket streaming content.
    /// </summary>
    /// <remarks>
    /// This content type is for real-time data streams over WebSocket connections.
    /// WebSocket adapters handle connection management, message framing, and stream aggregation.
    /// </remarks>
    WebSocket = 6,

    /// <summary>
    /// Server-Sent Events (SSE) streaming content.
    /// </summary>
    /// <remarks>
    /// This content type is for unidirectional server-to-client event streams.
    /// SSE adapters handle event parsing, reconnection, and stream processing.
    /// </remarks>
    ServerSentEvents = 7,

    /// <summary>
    /// Binary data content.
    /// </summary>
    /// <remarks>
    /// This content type is for binary files, images, PDFs, and other non-text content.
    /// Binary adapters handle byte-level operations and may convert to text where applicable.
    /// </remarks>
    Binary = 8,

    /// <summary>
    /// Plain text content without markup.
    /// </summary>
    /// <remarks>
    /// This content type is for simple text files or text/plain responses.
    /// Minimal processing is typically required for this content type.
    /// </remarks>
    PlainText = 9,

    /// <summary>
    /// Text content (alias for PlainText).
    /// </summary>
    Text = PlainText,

    /// <summary>
    /// CSV (Comma-Separated Values) content.
    /// </summary>
    /// <remarks>
    /// This content type is for tabular data in CSV format. CSV adapters handle
    /// parsing, delimiter detection, and may convert to structured formats.
    /// </remarks>
    Csv = 10,

    /// <summary>
    /// Markdown formatted content.
    /// </summary>
    /// <remarks>
    /// This content type is for Markdown documents. Adapters may parse Markdown
    /// syntax or convert to HTML for further processing.
    /// </remarks>
    Markdown = 11,

    /// <summary>
    /// RSS feed content.
    /// </summary>
    /// <remarks>
    /// This content type is specifically for RSS (Really Simple Syndication) feeds.
    /// RSS adapters parse feed structure and extract items, metadata, and content.
    /// </remarks>
    Rss = 12,

    /// <summary>
    /// Atom feed content.
    /// </summary>
    /// <remarks>
    /// This content type is specifically for Atom syndication format feeds.
    /// Atom adapters parse feed structure similar to RSS but with Atom-specific semantics.
    /// </remarks>
    Atom = 13,

    /// <summary>
    /// gRPC service content.
    /// </summary>
    /// <remarks>
    /// This content type is for gRPC services using Protocol Buffers.
    /// gRPC adapters handle binary serialization and service method invocation.
    /// </remarks>
    Grpc = 14,

    /// <summary>
    /// Custom or proprietary content type.
    /// </summary>
    /// <remarks>
    /// This content type is for custom formats that require specialized adapters.
    /// Use this when none of the predefined content types are suitable.
    /// </remarks>
    Custom = 99
}

/// <summary>
/// Provides extension methods for the <see cref="ContentType"/> enumeration.
/// </summary>
public static class ContentTypeExtensions
{
    /// <summary>
    /// Determines whether the content type represents a text-based format.
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns><c>true</c> if the content type is text-based; otherwise, <c>false</c>.</returns>
    public static bool IsTextBased(this ContentType contentType)
    {
        return contentType switch
        {
            ContentType.StaticHtml => true,
            ContentType.JavaScript => true,
            ContentType.Json => true,
            ContentType.Xml => true,
            ContentType.GraphQL => true,
            ContentType.PlainText => true,
            ContentType.Csv => true,
            ContentType.Markdown => true,
            ContentType.Rss => true,
            ContentType.Atom => true,
            _ => false
        };
    }

    /// <summary>
    /// Determines whether the content type represents a streaming format.
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns><c>true</c> if the content type is streaming; otherwise, <c>false</c>.</returns>
    public static bool IsStreaming(this ContentType contentType)
    {
        return contentType is ContentType.WebSocket or ContentType.ServerSentEvents;
    }

    /// <summary>
    /// Determines whether the content type represents a structured data format.
    /// </summary>
    /// <param name="contentType">The content type to check.</param>
    /// <returns><c>true</c> if the content type is structured; otherwise, <c>false</c>.</returns>
    public static bool IsStructured(this ContentType contentType)
    {
        return contentType switch
        {
            ContentType.Json => true,
            ContentType.Xml => true,
            ContentType.GraphQL => true,
            ContentType.Csv => true,
            ContentType.Rss => true,
            ContentType.Atom => true,
            ContentType.Grpc => true,
            _ => false
        };
    }

    /// <summary>
    /// Gets the typical MIME type associated with the content type.
    /// </summary>
    /// <param name="contentType">The content type.</param>
    /// <returns>The MIME type string, or null if not applicable.</returns>
    public static string? GetMimeType(this ContentType contentType)
    {
        return contentType switch
        {
            ContentType.StaticHtml => "text/html",
            ContentType.JavaScript => "text/html",
            ContentType.Json => "application/json",
            ContentType.Xml => "application/xml",
            ContentType.GraphQL => "application/json",
            ContentType.WebSocket => null,
            ContentType.ServerSentEvents => "text/event-stream",
            ContentType.PlainText => "text/plain",
            ContentType.Csv => "text/csv",
            ContentType.Markdown => "text/markdown",
            ContentType.Rss => "application/rss+xml",
            ContentType.Atom => "application/atom+xml",
            ContentType.Grpc => "application/grpc",
            _ => null
        };
    }

    /// <summary>
    /// Attempts to detect the content type from a MIME type string.
    /// </summary>
    /// <param name="mimeType">The MIME type string.</param>
    /// <returns>The detected <see cref="ContentType"/>, or <see cref="ContentType.Unknown"/> if not recognized.</returns>
    public static ContentType FromMimeType(string? mimeType)
    {
        if (string.IsNullOrWhiteSpace(mimeType))
        {
            return ContentType.Unknown;
        }

        return mimeType.ToLowerInvariant() switch
        {
            var mime when mime.Contains("text/html") => ContentType.StaticHtml,
            var mime when mime.Contains("application/json") => ContentType.Json,
            var mime when mime.Contains("application/xml") => ContentType.Xml,
            var mime when mime.Contains("text/xml") => ContentType.Xml,
            var mime when mime.Contains("text/event-stream") => ContentType.ServerSentEvents,
            var mime when mime.Contains("text/plain") => ContentType.PlainText,
            var mime when mime.Contains("text/csv") => ContentType.Csv,
            var mime when mime.Contains("text/markdown") => ContentType.Markdown,
            var mime when mime.Contains("application/rss+xml") => ContentType.Rss,
            var mime when mime.Contains("application/atom+xml") => ContentType.Atom,
            var mime when mime.Contains("application/grpc") => ContentType.Grpc,
            var mime when mime.StartsWith("image/") => ContentType.Binary,
            var mime when mime.StartsWith("video/") => ContentType.Binary,
            var mime when mime.StartsWith("audio/") => ContentType.Binary,
            var mime when mime.Contains("application/pdf") => ContentType.Binary,
            var mime when mime.Contains("application/octet-stream") => ContentType.Binary,
            _ => ContentType.Unknown
        };
    }
}
