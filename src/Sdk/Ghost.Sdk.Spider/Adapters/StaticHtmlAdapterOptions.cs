using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Adapters;

/// <summary>
/// Configuration options specific to the StaticHtmlAdapter.
/// </summary>
/// <remarks>
/// This class extends the base <see cref="AdapterOptions"/> with HTTP-specific
/// configuration options for making static HTML requests.
/// </remarks>
public class StaticHtmlAdapterOptions : AdapterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to enable automatic decompression of compressed responses.
    /// </summary>
    /// <value>
    /// <c>true</c> to automatically decompress gzip/deflate responses; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool EnableAutomaticDecompression { get; set; } = true;

    /// <summary>
    /// Gets or sets the proxy URL to use for requests.
    /// </summary>
    /// <value>The proxy server URL (e.g., "http://proxy.example.com:8080"), or null for no proxy.</value>
    /// <remarks>
    /// Supports HTTP and SOCKS proxies. Format: [protocol://][username:password@]host:port
    /// </remarks>
    public string? ProxyUrl { get; set; }

    /// <summary>
    /// Gets or sets the proxy username for authentication.
    /// </summary>
    /// <value>The username for proxy authentication, or null if not required.</value>
    public string? ProxyUsername { get; set; }

    /// <summary>
    /// Gets or sets the proxy password for authentication.
    /// </summary>
    /// <value>The password for proxy authentication, or null if not required.</value>
    public string? ProxyPassword { get; set; }

    /// <summary>
    /// Gets or sets cookies to include in requests.
    /// </summary>
    /// <value>A dictionary of cookie name-value pairs.</value>
    /// <remarks>
    /// Cookies are automatically managed across requests when using the same adapter instance.
    /// These cookies are merged with any Set-Cookie headers from responses.
    /// </remarks>
    public Dictionary<string, string> Cookies { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether to use HTTP/2 protocol.
    /// </summary>
    /// <value><c>true</c> to prefer HTTP/2; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool UseHttp2 { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to allow HTTP/3 protocol.
    /// </summary>
    /// <value><c>true</c> to allow HTTP/3; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    public bool AllowHttp3 { get; set; }

    /// <summary>
    /// Gets or sets the maximum response content buffer size in bytes.
    /// </summary>
    /// <value>The maximum buffer size. Defaults to 10MB.</value>
    /// <remarks>
    /// This limit prevents excessive memory consumption when downloading large responses.
    /// Set to null for unlimited buffering (not recommended).
    /// </remarks>
    public long? MaxResponseContentBufferSize { get; set; } = 10 * 1024 * 1024; // 10MB

    /// <summary>
    /// Gets or sets the connection lease timeout.
    /// </summary>
    /// <value>
    /// The duration a connection can be leased before it's refreshed. Defaults to 5 minutes.
    /// </value>
    /// <remarks>
    /// Periodic connection refresh helps avoid DNS staleness and load balancer issues.
    /// </remarks>
    public TimeSpan ConnectionLeaseTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets a value indicating whether to pool connections.
    /// </summary>
    /// <value><c>true</c> to enable connection pooling; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool PoolConnections { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of connections per server.
    /// </summary>
    /// <value>The maximum connection count per server endpoint. Defaults to 10.</value>
    public int MaxConnectionsPerServer { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether to expect 100-Continue response.
    /// </summary>
    /// <value>
    /// <c>true</c> to send Expect: 100-Continue header; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    public bool Expect100Continue { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to use default credentials.
    /// </summary>
    /// <value>
    /// <c>true</c> to use default system credentials for authentication; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    public bool UseDefaultCredentials { get; set; }

    /// <summary>
    /// Gets or sets the Accept header value.
    /// </summary>
    /// <value>
    /// The MIME types to accept in responses. Defaults to accepting HTML, XHTML, and XML.
    /// </value>
    public string AcceptHeader { get; set; } = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";

    /// <summary>
    /// Gets or sets the Accept-Language header value.
    /// </summary>
    /// <value>The preferred languages for responses. Defaults to English.</value>
    public string AcceptLanguage { get; set; } = "en-US,en;q=0.9";

    /// <summary>
    /// Gets or sets the Accept-Encoding header value.
    /// </summary>
    /// <value>The accepted content encodings. Defaults to gzip and deflate.</value>
    public string AcceptEncoding { get; set; } = "gzip, deflate";

    /// <summary>
    /// Initializes a new instance of the <see cref="StaticHtmlAdapterOptions"/> class.
    /// </summary>
    public StaticHtmlAdapterOptions()
    {
    }

    /// <summary>
    /// Validates the StaticHtmlAdapter-specific options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    public override void Validate()
    {
        base.Validate();

        if (MaxConnectionsPerServer <= 0)
        {
            throw new ArgumentException("MaxConnectionsPerServer must be greater than zero.", nameof(MaxConnectionsPerServer));
        }

        if (ConnectionLeaseTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("ConnectionLeaseTimeout must be greater than zero.", nameof(ConnectionLeaseTimeout));
        }

        if (MaxResponseContentBufferSize.HasValue && MaxResponseContentBufferSize.Value <= 0)
        {
            throw new ArgumentException("MaxResponseContentBufferSize must be greater than zero when specified.", nameof(MaxResponseContentBufferSize));
        }

        if (!string.IsNullOrEmpty(ProxyUrl))
        {
            if (!Uri.TryCreate(ProxyUrl, UriKind.Absolute, out _))
            {
                throw new ArgumentException("ProxyUrl must be a valid absolute URI.", nameof(ProxyUrl));
            }
        }

        if (string.IsNullOrWhiteSpace(AcceptHeader))
        {
            throw new ArgumentException("AcceptHeader cannot be null or whitespace.", nameof(AcceptHeader));
        }

        if (string.IsNullOrWhiteSpace(AcceptLanguage))
        {
            throw new ArgumentException("AcceptLanguage cannot be null or whitespace.", nameof(AcceptLanguage));
        }

        if (string.IsNullOrWhiteSpace(AcceptEncoding))
        {
            throw new ArgumentException("AcceptEncoding cannot be null or whitespace.", nameof(AcceptEncoding));
        }
    }

    /// <summary>
    /// Creates a copy of the current options instance.
    /// </summary>
    /// <returns>A new instance with the same configuration values.</returns>
    public override AdapterOptions Clone()
    {
        var clone = (StaticHtmlAdapterOptions)base.Clone();
        clone.Cookies = new Dictionary<string, string>(Cookies);
        return clone;
    }
}
