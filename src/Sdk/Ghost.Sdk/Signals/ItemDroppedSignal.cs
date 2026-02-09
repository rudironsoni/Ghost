namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when an item is dropped (validation failed, duplicate, etc.).
/// </summary>
public sealed record ItemDroppedSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemDroppedSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the item was dropped.</param>
    /// <param name="itemType">The type name of the dropped item.</param>
    /// <param name="url">The URL where the item was scraped from.</param>
    /// <param name="reason">The reason the item was dropped.</param>
    public ItemDroppedSignal(string spiderId, DateTimeOffset timestamp, string itemType, string url, string reason)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        ArgumentNullException.ThrowIfNull(itemType);
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(reason);
        SpiderId = spiderId;
        Timestamp = timestamp;
        ItemType = itemType;
        Url = url;
        Reason = reason;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string SpiderId { get; }

    /// <summary>
    /// Gets the type name of the dropped item.
    /// </summary>
    public string ItemType { get; }

    /// <summary>
    /// Gets the URL where the item was scraped from.
    /// </summary>
    public string Url { get; }

    /// <summary>
    /// Gets the reason the item was dropped.
    /// </summary>
    public string Reason { get; }
}
