namespace Ghost.Sdk.Spider.Strategies;

/// <summary>
/// Represents the context for strategy execution, including state and configuration.
/// </summary>
public class StrategyContext
{
    /// <summary>
    /// Gets or sets the URL to extract data from.
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Gets or sets the raw content if already fetched.
    /// </summary>
    public string? Content { get; init; }

    /// <summary>
    /// Gets or sets the content type.
    /// </summary>
    public string ContentType { get; init; } = "text/html";

    /// <summary>
    /// Gets or sets the HTTP status code from the response.
    /// </summary>
    public int? StatusCode { get; init; }

    /// <summary>
    /// Gets or sets the request headers.
    /// </summary>
    public Dictionary<string, string> Headers { get; init; } = new();

    /// <summary>
    /// Gets or sets additional parameters for the extraction.
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();

    /// <summary>
    /// Gets or sets the state that can be shared across strategy attempts.
    /// </summary>
    public Dictionary<string, object> State { get; init; } = new();

    /// <summary>
    /// Gets or sets the maximum time to wait for extraction.
    /// </summary>
    public TimeSpan? Timeout { get; init; }

    /// <summary>
    /// Gets or sets the retry count for the current strategy.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries allowed.
    /// </summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>
    /// Gets or sets the timestamp when the context was created.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets custom metadata for the context.
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Gets or sets the user agent string for requests.
    /// </summary>
    public string? UserAgent { get; init; }

    /// <summary>
    /// Gets or sets a value indicating whether to follow redirects.
    /// </summary>
    public bool FollowRedirects { get; init; } = true;

    /// <summary>
    /// Gets or sets the previous extraction result if this is a retry.
    /// </summary>
    public ExtractionResult? PreviousResult { get; set; }

    /// <summary>
    /// Creates a copy of this context with updated properties.
    /// </summary>
    /// <param name="configure">Action to configure the new context.</param>
    /// <returns>A new <see cref="StrategyContext"/> instance.</returns>
    public StrategyContext WithModifications(Action<StrategyContextBuilder> configure)
    {
        var builder = new StrategyContextBuilder(this);
        configure(builder);
        return builder.Build();
    }
}

/// <summary>
/// Builder for creating modified strategy contexts.
/// </summary>
public class StrategyContextBuilder
{
    private string _url;
    private string? _content;
    private string _contentType;
    private int? _statusCode;
    private Dictionary<string, string> _headers;
    private Dictionary<string, object> _parameters;
    private Dictionary<string, object> _state;
    private TimeSpan? _timeout;
    private int _retryCount;
    private int _maxRetries;
    private Dictionary<string, object> _metadata;
    private string? _userAgent;
    private bool _followRedirects;
    private ExtractionResult? _previousResult;

    /// <summary>
    /// Initializes a new instance of the <see cref="StrategyContextBuilder"/> class.
    /// </summary>
    /// <param name="context">The context to copy properties from.</param>
    public StrategyContextBuilder(StrategyContext context)
    {
        _url = context.Url;
        _content = context.Content;
        _contentType = context.ContentType;
        _statusCode = context.StatusCode;
        _headers = new Dictionary<string, string>(context.Headers);
        _parameters = new Dictionary<string, object>(context.Parameters);
        _state = new Dictionary<string, object>(context.State);
        _timeout = context.Timeout;
        _retryCount = context.RetryCount;
        _maxRetries = context.MaxRetries;
        _metadata = new Dictionary<string, object>(context.Metadata);
        _userAgent = context.UserAgent;
        _followRedirects = context.FollowRedirects;
        _previousResult = context.PreviousResult;
    }

    /// <summary>
    /// Sets the URL.
    /// </summary>
    /// <param name="url">The URL.</param>
    /// <returns>This builder instance.</returns>
    public StrategyContextBuilder WithUrl(string url)
    {
        _url = url;
        return this;
    }

    /// <summary>
    /// Sets the content.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <returns>This builder instance.</returns>
    public StrategyContextBuilder WithContent(string? content)
    {
        _content = content;
        return this;
    }

    /// <summary>
    /// Sets the retry count.
    /// </summary>
    /// <param name="retryCount">The retry count.</param>
    /// <returns>This builder instance.</returns>
    public StrategyContextBuilder WithRetryCount(int retryCount)
    {
        _retryCount = retryCount;
        return this;
    }

    /// <summary>
    /// Sets the previous result.
    /// </summary>
    /// <param name="result">The previous result.</param>
    /// <returns>This builder instance.</returns>
    public StrategyContextBuilder WithPreviousResult(ExtractionResult? result)
    {
        _previousResult = result;
        return this;
    }

    /// <summary>
    /// Adds a state value.
    /// </summary>
    /// <param name="key">The state key.</param>
    /// <param name="value">The state value.</param>
    /// <returns>This builder instance.</returns>
    public StrategyContextBuilder AddState(string key, object value)
    {
        _state[key] = value;
        return this;
    }

    /// <summary>
    /// Builds the strategy context.
    /// </summary>
    /// <returns>A new <see cref="StrategyContext"/> instance.</returns>
    public StrategyContext Build()
    {
        return new StrategyContext
        {
            Url = _url,
            Content = _content,
            ContentType = _contentType,
            StatusCode = _statusCode,
            Headers = _headers,
            Parameters = _parameters,
            State = _state,
            Timeout = _timeout,
            RetryCount = _retryCount,
            MaxRetries = _maxRetries,
            Metadata = _metadata,
            UserAgent = _userAgent,
            FollowRedirects = _followRedirects,
            PreviousResult = _previousResult
        };
    }
}
