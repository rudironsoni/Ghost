namespace Ghost.Sdk.Pipelines;

/// <summary>
/// Represents a downloaded media item.
/// </summary>
public class MediaItem
{
    /// <summary>
    /// Gets or sets the original URL of the media.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the local file path where the media was saved.
    /// </summary>
    public string LocalPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the content type (MIME type).
    /// </summary>
    public string ContentType { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the SHA256 checksum of the downloaded file.
    /// </summary>
    public string? Checksum { get; set; }
}
