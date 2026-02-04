namespace Ghost.Sdk.Spider.Adapters.Contracts;

/// <summary>
/// Represents a request for content extraction.
/// </summary>
/// <remarks>
/// This class encapsulates all information needed to make a content extraction request,
/// including the target URL, HTTP headers, expected content type, and additional metadata
/// that adapters may use to optimize extraction.
/// </remarks>
public class Request
{
    /// <summary>
    /// Gets or sets the URL to extract content from.
    /// </summary>
    /// <value>The target URL for content extraction.</value>
    /// <remarks>
    /// This is the primary target for the extraction operation. The URL scheme may
    /// help determine which adapter should handle the request (http/https for web,
    /// ws/wss for WebSocket, etc.).
    /// </remarks>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the HTTP method to use for the request.
    /// </summary>
    /// <value>The HTTP method (e.g., "GET", "POST"). Defaults to "GET".</value>
    public string Method { get; set; } = "GET";

    /// <summary>
    /// Gets or sets the HTTP headers to include in the request.
    /// </summary>
    /// <value>A dictionary of HTTP header name-value pairs.</value>
    /// <remarks>
    /// Common headers include:
    /// <list type="bullet">
    /// <item>User-Agent: Browser identification</item>
    /// <item>Accept: Acceptable content types</item>
    /// <item>Authorization: Authentication credentials</item>
    /// <item>Referer: Source page URL</item>
    /// </list>
    /// </remarks>
    public Dictionary<string, string> Headers { get; set; } = new();

    /// <summary>
    /// Gets or sets the request body for POST/PUT requests.
    /// </summary>
    /// <value>The request body content, or null for GET requests.</value>
    public string? Body { get; set; }

    /// <summary>
    /// Gets or sets the expected content type.
    /// </summary>
    /// <value>The type of content expected from the request.</value>
    /// <remarks>
    /// This hint helps the adapter factory select the appropriate adapter and
    /// allows adapters to optimize their extraction strategy. If set to
    /// <see cref="ContentType.Unknown"/>, the adapter may attempt to auto-detect
    /// the content type.
    /// </remarks>
    public ContentType ExpectedContentType { get; set; } = ContentType.Unknown;

    /// <summary>
    /// Gets or sets the timeout duration for the request.
    /// </summary>
    /// <value>
    /// The maximum time to wait for content extraction. Defaults to 30 seconds.
    /// </value>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets a value indicating whether to follow redirects.
    /// </summary>
    /// <value><c>true</c> to follow HTTP redirects; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of redirects to follow.
    /// </summary>
    /// <value>The maximum redirect count. Defaults to 10.</value>
    public int MaxRedirects { get; set; } = 10;

    /// <summary>
    /// Gets or sets additional metadata for the request.
    /// </summary>
    /// <value>
    /// A dictionary of metadata key-value pairs that adapters may use to customize
    /// their behavior.
    /// </value>
    /// <remarks>
    /// Metadata may include:
    /// <list type="bullet">
    /// <item>"Priority": Request priority level</item>
    /// <item>"CacheKey": Cache key for storing/retrieving results</item>
    /// <item>"RetryPolicy": Custom retry policy identifier</item>
    /// <item>"AdapterPreference": Preferred adapter name</item>
    /// <item>"ExtractionRules": Custom extraction rules or selectors</item>
    /// </list>
    /// </remarks>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the unique identifier for this request.
    /// </summary>
    /// <value>A unique identifier for tracking and logging purposes.</value>
    public string RequestId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets the timestamp when the request was created.
    /// </summary>
    /// <value>The UTC timestamp of request creation.</value>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Initializes a new instance of the <see cref="Request"/> class.
    /// </summary>
    public Request()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Request"/> class with the specified URL.
    /// </summary>
    /// <param name="url">The target URL for content extraction.</param>
    public Request(string url)
    {
        Url = url;
    }

    /// <summary>
    /// Creates a GET request for the specified URL.
    /// </summary>
    /// <param name="url">The target URL.</param>
    /// <returns>A new <see cref="Request"/> instance configured for a GET operation.</returns>
    public static Request Get(string url)
    {
        return new Request(url) { Method = "GET" };
    }

    /// <summary>
    /// Creates a POST request for the specified URL with the given body.
    /// </summary>
    /// <param name="url">The target URL.</param>
    /// <param name="body">The request body content.</param>
    /// <returns>A new <see cref="Request"/> instance configured for a POST operation.</returns>
    public static Request Post(string url, string body)
    {
        return new Request(url)
        {
            Method = "POST",
            Body = body
        };
    }

    /// <summary>
    /// Adds a header to the request.
    /// </summary>
    /// <param name="name">The header name.</param>
    /// <param name="value">The header value.</param>
    /// <returns>This <see cref="Request"/> instance for method chaining.</returns>
    public Request WithHeader(string name, string value)
    {
        Headers[name] = value;
        return this;
    }

    /// <summary>
    /// Adds metadata to the request.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>This <see cref="Request"/> instance for method chaining.</returns>
    public Request WithMetadata(string key, object value)
    {
        Metadata[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the expected content type for the request.
    /// </summary>
    /// <param name="contentType">The expected content type.</param>
    /// <returns>This <see cref="Request"/> instance for method chaining.</returns>
    public Request ExpectingContentType(ContentType contentType)
    {
        ExpectedContentType = contentType;
        return this;
    }

    /// <summary>
    /// Sets the timeout for the request.
    /// </summary>
    /// <param name="timeout">The timeout duration.</param>
    /// <returns>This <see cref="Request"/> instance for method chaining.</returns>
    public Request WithTimeout(TimeSpan timeout)
    {
        Timeout = timeout;
        return this;
    }
}
