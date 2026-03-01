namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when a request is scheduled for execution.
/// </summary>
public sealed record RequestScheduledSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestScheduledSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the request was scheduled.</param>
    /// <param name="url">The URL of the scheduled request.</param>
    /// <param name="method">The HTTP method.</param>
    public RequestScheduledSignal(string spiderId, DateTimeOffset timestamp, string url, string method = "GET")
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(method);
        SpiderId = spiderId;
        Timestamp = timestamp;
        Url = url;
        Method = method;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string SpiderId { get; }

    /// <summary>
    /// Gets the URL of the scheduled request.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Gets the HTTP method.
    /// </summary>
    public string Method { get; }
}
