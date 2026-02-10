namespace Ghost.Sdk.Spiders;

/// <summary>
/// Base class for items extracted by spiders.
/// </summary>
/// <remarks>
/// Items represent data extracted from web pages. They can contain any structured data
/// and are typically processed by pipelines for validation, cleaning, and storage.
/// </remarks>
public class Item
{
    /// <summary>
    /// Gets or sets the metadata associated with this item.
    /// </summary>
    /// <value>A dictionary of metadata key-value pairs.</value>
    /// <remarks>
    /// Metadata can include information such as:
    /// <list type="bullet">
    /// <item>Source URL where the item was extracted</item>
    /// <item>Timestamp of extraction</item>
    /// <item>Spider name that extracted the item</item>
    /// <item>Custom extraction metadata</item>
    /// </list>
    /// </remarks>
    public Dictionary<string, object> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the source URL where this item was extracted.
    /// </summary>
    /// <value>The URL of the page from which this item was extracted.</value>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the timestamp when this item was extracted.
    /// </summary>
    /// <value>The UTC timestamp of item extraction.</value>
    public DateTimeOffset ExtractedAt { get; set; } = DateTimeOffset.UtcNow;
}
