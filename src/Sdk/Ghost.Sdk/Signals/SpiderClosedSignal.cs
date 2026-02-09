namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when a spider is closed.
/// </summary>
public sealed record SpiderClosedSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpiderClosedSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the spider was closed.</param>
    /// <param name="reason">Optional reason for closure.</param>
    public SpiderClosedSignal(string spiderId, DateTimeOffset timestamp, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        SpiderId = spiderId;
        Timestamp = timestamp;
        Reason = reason;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string SpiderId { get; }

    /// <summary>
    /// Gets the optional reason for closure.
    /// </summary>
    public string? Reason { get; }
}
