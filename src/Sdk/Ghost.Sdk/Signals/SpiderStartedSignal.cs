namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when a spider starts.
/// </summary>
public sealed record SpiderStartedSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpiderStartedSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the spider started.</param>
    public SpiderStartedSignal(string spiderId, DateTimeOffset timestamp)
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
