namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when a spider becomes idle (no pending requests).
/// </summary>
public sealed record SpiderIdleSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpiderIdleSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the spider became idle.</param>
    public SpiderIdleSignal(string spiderId, DateTimeOffset timestamp)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        SpiderId = spiderId;
        Timestamp = timestamp;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string SpiderId { get; }
}
