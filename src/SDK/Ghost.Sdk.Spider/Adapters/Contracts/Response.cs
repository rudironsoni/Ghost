using Ghost.Sdk.Spider.Meta;

namespace Ghost.Sdk.Spider.Adapters.Contracts;

/// <summary>
/// Represents the response from a content extraction operation.
/// </summary>
/// <remarks>
/// This class encapsulates the complete response from an adapter, including the
/// extracted content, HTTP response metadata, timing information, and any errors
/// that occurred during extraction.
/// </remarks>
public class Response
{
    /// <summary>
    /// Gets or sets the content result from the extraction operation.
    /// </summary>
    /// <value>The extracted content and associated metadata.</value>
    public ContentResult Content { get; set; } = new();

    /// <summary>
    /// Gets or sets the HTTP status code from the response.
    /// </summary>
    /// <value>The HTTP status code, or null if not applicable.</value>
    /// <remarks>
    /// This property is relevant for HTTP-based adapters. For other adapter types
    /// (WebSocket, GraphQL, etc.), this may be null or represent an equivalent status.
    /// </remarks>
    public int? StatusCode { get; set; }

    /// <summary>
    /// Gets or sets the reason phrase associated with the status code.
    /// </summary>
    /// <value>The HTTP reason phrase (e.g., "OK", "Not Found").</value>
    public string? ReasonPhrase { get; set; }

    /// <summary>
    /// Gets or sets the response headers.
    /// </summary>
    /// <value>A dictionary of response header name-value pairs.</value>
    /// <remarks>
    /// Common response headers include:
    /// <list type="bullet">
    /// <item>Content-Type: MIME type of the response</item>
    /// <item>Content-Length: Size of the response body</item>
    /// <item>Cache-Control: Caching directives</item>
    /// <item>ETag: Resource version identifier</item>
    /// <item>Last-Modified: Last modification timestamp</item>
    /// </list>
    /// </remarks>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the final URL after any redirects.
    /// </summary>
    /// <value>
    /// The final URL that was used to retrieve content, which may differ from
    /// the original request URL if redirects were followed.
    /// </value>
    public string? FinalUrl { get; set; }

    /// <summary>
    /// Gets or sets the name of the adapter that processed the request.
    /// </summary>
    /// <value>The adapter name (e.g., "StaticHtml", "JavaScript", "GraphQL").</value>
    public string? AdapterName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the extraction was successful.
    /// </summary>
    /// <value>
    /// <c>true</c> if content was successfully extracted; otherwise, <c>false</c>.
    /// </value>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets error information if the extraction failed.
    /// </summary>
    /// <value>A description of the error, or null if successful.</value>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the exception that occurred during extraction, if any.
    /// </summary>
    /// <value>The exception that caused the extraction to fail, or null if successful.</value>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the request was sent.
    /// </summary>
    /// <value>The UTC timestamp when extraction started.</value>
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the timestamp when the response was received.
    /// </summary>
    /// <value>The UTC timestamp when extraction completed.</value>
    public DateTimeOffset RespondedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets the duration of the extraction operation.
    /// </summary>
    /// <value>The time elapsed between request and response.</value>
    public TimeSpan Duration => RespondedAt - RequestedAt;

    /// <summary>
    /// Gets or sets the number of redirect hops that were followed.
    /// </summary>
    /// <value>The count of HTTP redirects followed, or null if not applicable.</value>
    public int? RedirectCount { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the content was served from cache.
    /// </summary>
    /// <value>
    /// <c>true</c> if the content was retrieved from cache; otherwise, <c>false</c>.
    /// </value>
    public bool FromCache { get; set; }

    /// <summary>
    /// Gets or sets additional metadata about the response.
    /// </summary>
    /// <value>
    /// A dictionary of metadata key-value pairs with adapter-specific information,
    /// performance metrics, or debugging data.
    /// </value>
    /// <remarks>
    /// Metadata may include:
    /// <list type="bullet">
    /// <item>"BytesReceived": Total bytes received</item>
    /// <item>"BytesSent": Total bytes sent</item>
    /// <item>"ConnectionReuseCount": Number of times connection was reused</item>
    /// <item>"DnsLookupTime": Time spent on DNS lookup</item>
    /// <item>"SslHandshakeTime": Time spent on SSL/TLS handshake</item>
    /// <item>"RetryCount": Number of retry attempts</item>
    /// </list>
    /// </remarks>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets the type-safe metadata dictionary for storing spider metadata.
    /// </summary>
    /// <value>
    /// A type-safe dictionary for storing custom metadata that can be passed
    /// between spider components, such as response metadata or extracted data.
    /// </value>
    /// <remarks>
    /// Example usage:
    /// <code>
    /// response.Meta.Set("depth", 3);
    /// response.Meta.Set("parent_url", "https://example.com");
    /// var depth = response.Meta.Get&lt;int&gt;("depth");
    /// </code>
    /// </remarks>
    public IMetaDictionary Meta { get; } = new MetaDictionary();

    /// <summary>
    /// Initializes a new instance of the <see cref="Response"/> class.
    /// </summary>
    public Response()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Response"/> class with the specified content result.
    /// </summary>
    /// <param name="contentResult">The content extraction result.</param>
    public Response(ContentResult contentResult)
    {
        Content = contentResult;
        IsSuccess = contentResult.Success;
        Error = contentResult.Error;
    }

    /// <summary>
    /// Creates a successful response with the specified content.
    /// </summary>
    /// <param name="content">The extracted content.</param>
    /// <param name="contentType">The type of the content.</param>
    /// <returns>A new <see cref="Response"/> instance marked as successful.</returns>
    public static Response Success(string content, ContentType contentType)
    {
        var contentResult = ContentResult.CreateSuccess(content, contentType);
        return new Response(contentResult)
        {
            IsSuccess = true,
            StatusCode = 200,
            ReasonPhrase = "OK"
        };
    }

    /// <summary>
    /// Creates a failed response with the specified error message.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="exception">The exception that caused the failure, if any.</param>
    /// <returns>A new <see cref="Response"/> instance marked as failed.</returns>
    public static Response Failure(string error, Exception? exception = null)
    {
        var contentResult = ContentResult.CreateFailure(error);
        return new Response(contentResult)
        {
            IsSuccess = false,
            Error = error,
            Exception = exception
        };
    }

    /// <summary>
    /// Adds a header to the response.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>This <see cref="Response"/> instance for method chaining.</returns>
    public Response WithHeader(string name, string value)
    {
        Headers[name] = value;
        return this;
    }

    /// <summary>
    /// Adds metadata to the response.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>This <see cref="Response"/> instance for method chaining.</returns>
    public Response WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the adapter name that processed the request.
    /// </summary>
    /// <param name="adapterName">The adapter name.</param>
    /// <returns>This <see cref="Response"/> instance for method chaining.</returns>
    public Response FromAdapter(string adapterName)
    {
        AdapterName = adapterName;
        return this;
    }
}
