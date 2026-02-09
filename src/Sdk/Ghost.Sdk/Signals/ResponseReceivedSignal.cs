namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when a response is received from a request.
/// </summary>
public sealed record ResponseReceivedSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResponseReceivedSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the response was received.</param>
    /// <param name="url">The URL of the response.</param>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="durationMs">The request duration in milliseconds.</param>
    public ResponseReceivedSignal(string spiderId, DateTimeOffset timestamp, string url, int statusCode, long durationMs)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        ArgumentNullException.ThrowIfNull(url);
        SpiderId = spiderId;
        Timestamp = timestamp;
        Url = url;
        StatusCode = statusCode;
        DurationMs = durationMs;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string SpiderId { get; }

    /// <summary>
    /// Gets the URL of the response.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Gets the HTTP status code.
    /// </summary>
    public int StatusCode { get; }

    /// <summary>
    /// Gets the request duration in milliseconds.
    /// </summary>
    public long DurationMs { get; }
}
