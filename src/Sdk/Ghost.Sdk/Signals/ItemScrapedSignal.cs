namespace Ghost.Sdk.Signals;

/// <summary>
/// Signal emitted when an item is successfully scraped.
/// </summary>
public sealed record ItemScrapedSignal : ISignal
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ItemScrapedSignal"/> class.
    /// </summary>
    /// <param name="spiderId">The spider identifier.</param>
    /// <param name="timestamp">The timestamp when the item was scraped.</param>
    /// <param name="itemType">The type name of the scraped item.</param>
    /// <param name="url">The URL where the item was scraped from.</param>
    public ItemScrapedSignal(string spiderId, DateTimeOffset timestamp, string itemType, string url)
    {
        ArgumentNullException.ThrowIfNull(spiderId);
        ArgumentNullException.ThrowIfNull(itemType);
        ArgumentNullException.ThrowIfNull(url);
        SpiderId = spiderId;
        Timestamp = timestamp;
        ItemType = itemType;
        Url = url;
    }

    /// <inheritdoc/>
    public DateTimeOffset Timestamp { get; }

    /// <inheritdoc/>
    public string SpiderId { get; }

    /// <summary>
    /// Gets the type name of the scraped item.
    /// </summary>
    public string ItemType { get; }

    /// <summary>
    /// Gets the URL where the item was scraped from.
    /// </summary>
    public string Url { get; }
}
