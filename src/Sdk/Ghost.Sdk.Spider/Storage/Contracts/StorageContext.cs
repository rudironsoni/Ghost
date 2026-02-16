namespace Ghost.Sdk.Spider.Storage.Contracts;

/// <summary>
/// Provides context information for storage operations.
/// </summary>
/// <remarks>
/// The storage context carries metadata about the extraction operation that can
/// be used by storage implementations to organize, tag, or filter data.
/// </remarks>
public class StorageContext
{
    /// <summary>
    /// Gets or sets the spider name that extracted the data.
    /// </summary>
    /// <value>The spider identifier.</value>
    public string? SpiderName { get; set; }

    /// <summary>
    /// Gets or sets the source URL where the data was extracted from.
    /// </summary>
    /// <value>The source URL.</value>
    public string? SourceUrl { get; set; }

    /// <summary>
    /// Gets or sets the extraction timestamp.
    /// </summary>
    /// <value>The UTC timestamp of extraction.</value>
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the table or collection name for storage.
    /// </summary>
    /// <value>The destination table/collection name.</value>
    public string? TableName { get; set; }

    /// <summary>
    /// Gets or sets custom metadata for the storage operation.
    /// </summary>
    /// <value>Dictionary of custom key-value pairs.</value>
    public Dictionary<string, object> Metadata { get; set; } = [];

    /// <summary>
    /// Gets or sets tags for categorizing the stored data.
    /// </summary>
    /// <value>List of tags.</value>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets the batch identifier for grouped operations.
    /// </summary>
    /// <value>The batch ID, or null if not part of a batch.</value>
    public string? BatchId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to update existing items.
    /// </summary>
    /// <value><c>true</c> to update on conflict; otherwise, <c>false</c>. Defaults to <c>false</c>.</value>
    public bool UpdateOnConflict { get; set; }

    /// <summary>
    /// Gets or sets the unique key field names for conflict detection.
    /// </summary>
    /// <value>List of field names that form the unique key.</value>
    public List<string> UniqueKeys { get; set; } = [];

    /// <summary>
    /// Creates a new storage context with the specified spider name.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <returns>A new <see cref="StorageContext"/> instance.</returns>
    public static StorageContext Create(string spiderName)
    {
        return new StorageContext
        {
            SpiderName = spiderName,
            Timestamp = DateTimeOffset.UtcNow
        };
    }

    /// <summary>
    /// Creates a new storage context with the specified spider name and source URL.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <param name="sourceUrl">The source URL.</param>
    /// <returns>A new <see cref="StorageContext"/> instance.</returns>
    public static StorageContext Create(string spiderName, string sourceUrl)
    {
        return new StorageContext
        {
            SpiderName = spiderName,
            SourceUrl = sourceUrl,
            Timestamp = DateTimeOffset.UtcNow
        };
    }
}
