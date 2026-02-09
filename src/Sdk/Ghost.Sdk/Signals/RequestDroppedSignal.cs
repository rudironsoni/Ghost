namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when a request is dropped (filtered out, rate limited, etc.).
/// </summary>
public sealed record RequestDroppedSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RequestDroppedSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the request was dropped.</param>
    /// <param name="url">The URL of the dropped request.</param>
    /// <param name="reason">The reason the request was dropped.</param>
    public RequestDroppedSignal(string spiderId, DateTimeOffset timestamp, string url, string reason)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(reason);
        SpiderId = spiderId;
        Timestamp = timestamp;
        Url = url;
        Reason = reason;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string SpiderId { get; }

    /// <summary>
    /// Gets the URL of the dropped request.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Gets the reason the request was dropped.
    /// </summary>
    public string Reason { get; }
}
