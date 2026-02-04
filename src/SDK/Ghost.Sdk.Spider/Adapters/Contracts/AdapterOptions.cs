namespace Ghost.Sdk.Spider.Adapters.Contracts;

/// <summary>
/// Base class for adapter-specific options that control content extraction behavior.
/// </summary>
/// <remarks>
/// This abstract class provides common configuration options for all adapters.
/// Specific adapter implementations should derive from this class to add their
/// own configuration properties while maintaining consistency across adapters.
/// </remarks>
public abstract class AdapterOptions
{
    /// <summary>
    /// Gets or sets the timeout duration for extraction operations.
    /// </summary>
    /// <value>
    /// The maximum time to wait for content extraction. Defaults to 30 seconds.
    /// </value>
    /// <remarks>
    /// This timeout applies to the entire extraction operation, including any
    /// retries. Individual adapters may apply this timeout differently based on
    /// their implementation specifics.
    /// </remarks>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the maximum number of retry attempts for failed operations.
    /// </summary>
    /// <value>The maximum retry count. Defaults to 3.</value>
    /// <remarks>
    /// Retries are typically attempted for transient failures such as network
    /// timeouts or temporary server errors. The retry behavior may be influenced
    /// by the <see cref="RetryDelay"/> property.
    /// </remarks>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the delay between retry attempts.
    /// </summary>
    /// <value>The delay duration between retries. Defaults to 1 second.</value>
    /// <remarks>
    /// Some adapters may implement exponential backoff or other retry strategies
    /// that use this value as a base delay.
    /// </remarks>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Gets or sets a value indicating whether to use exponential backoff for retries.
    /// </summary>
    /// <value>
    /// <c>true</c> to use exponential backoff; <c>false</c> for fixed delay. Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, retry delays increase exponentially (e.g., 1s, 2s, 4s, 8s) to
    /// reduce load on failing services and increase the likelihood of recovery.
    /// </remarks>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to follow HTTP redirects.
    /// </summary>
    /// <value><c>true</c> to follow redirects; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    public bool FollowRedirects { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of redirects to follow.
    /// </summary>
    /// <value>The maximum redirect count. Defaults to 10.</value>
    public int MaxRedirects { get; set; } = 10;

    /// <summary>
    /// Gets or sets a value indicating whether to validate SSL certificates.
    /// </summary>
    /// <value>
    /// <c>true</c> to validate SSL certificates; <c>false</c> to accept any certificate.
    /// Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// <para>
    /// <strong>Security Warning:</strong> Disabling SSL validation can expose your
    /// application to man-in-the-middle attacks. Only disable this for testing or
    /// when accessing known internal services with self-signed certificates.
    /// </para>
    /// </remarks>
    public bool ValidateSslCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets the User-Agent string to use for requests.
    /// </summary>
    /// <value>
    /// The User-Agent header value. Defaults to a standard browser user agent.
    /// </value>
    /// <remarks>
    /// Some websites may block requests with non-browser user agents. Using a
    /// realistic user agent can help avoid being blocked, but be respectful of
    /// robots.txt and website terms of service.
    /// </remarks>
    public string UserAgent { get; set; } = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

    /// <summary>
    /// Gets or sets additional HTTP headers to include in requests.
    /// </summary>
    /// <value>A dictionary of custom HTTP headers.</value>
    /// <remarks>
    /// These headers are added to all requests made by the adapter. Common use cases
    /// include authentication tokens, custom tracking headers, or API keys.
    /// </remarks>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to enable caching.
    /// </summary>
    /// <value><c>true</c> to enable caching; otherwise, <c>false</c>. Defaults to <c>true</c>.</value>
    /// <remarks>
    /// When enabled, adapters may cache responses to reduce redundant requests.
    /// Cache behavior depends on the specific adapter implementation and may respect
    /// standard HTTP caching headers.
    /// </remarks>
    public bool EnableCaching { get; set; } = true;

    /// <summary>
    /// Gets or sets the cache time-to-live duration.
    /// </summary>
    /// <value>
    /// The duration to keep cached content before it expires. Defaults to 5 minutes.
    /// </value>
    /// <remarks>
    /// This value may be overridden by HTTP cache-control headers if the adapter
    /// respects standard caching directives.
    /// </remarks>
    public TimeSpan CacheTtl { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets additional metadata for the options.
    /// </summary>
    /// <value>
    /// A dictionary of metadata key-value pairs for adapter-specific configuration.
    /// </value>
    /// <remarks>
    /// Derived adapter classes can use this dictionary to store configuration
    /// values that don't warrant dedicated properties, or to pass through
    /// configuration to underlying libraries.
    /// </remarks>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="AdapterOptions"/> class.
    /// </summary>
    protected AdapterOptions()
    {
    }

    /// <summary>
    /// Validates the options configuration.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    /// <remarks>
    /// Derived classes should override this method to add validation for their
    /// specific configuration properties. Always call the base implementation.
    /// </remarks>
    public virtual void Validate()
    {
        if (Timeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("Timeout must be greater than zero.", nameof(Timeout));
        }

        if (MaxRetries < 0)
        {
            throw new ArgumentException("MaxRetries cannot be negative.", nameof(MaxRetries));
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            throw new ArgumentException("RetryDelay cannot be negative.", nameof(RetryDelay));
        }

        if (MaxRedirects < 0)
        {
            throw new ArgumentException("MaxRedirects cannot be negative.", nameof(MaxRedirects));
        }

        if (string.IsNullOrWhiteSpace(UserAgent))
        {
            throw new ArgumentException("UserAgent cannot be null or whitespace.", nameof(UserAgent));
        }

        if (CacheTtl < TimeSpan.Zero)
        {
            throw new ArgumentException("CacheTtl cannot be negative.", nameof(CacheTtl));
        }
    }

    /// <summary>
    /// Creates a copy of the current options instance.
    /// </summary>
    /// <returns>A new instance with the same configuration values.</returns>
    /// <remarks>
    /// Derived classes should override this method to ensure all properties are copied,
    /// including those specific to the derived type.
    /// </remarks>
    public virtual AdapterOptions Clone()
    {
        var clone = (AdapterOptions)MemberwiseClone();
        clone.CustomHeaders = new Dictionary<string, string>(CustomHeaders);
        clone.Metadata = new Dictionary<string, object>(Metadata);
        return clone;
    }
}
