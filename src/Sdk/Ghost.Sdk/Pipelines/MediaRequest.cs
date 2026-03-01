namespace Ghost.Sdk.Pipelines;

/// <summary>
/// Represents a media download request.
/// </summary>
public class MediaRequest
{
    /// <summary>
    /// Gets or sets the URL of the media to download.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the optional filename for the downloaded file.
    /// If not specified, will be extracted from URL.
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// Gets or sets the optional content type.
    /// </summary>
    public string? ContentType { get; set; }

    /// <summary>
    /// Gets or sets the output directory path for downloaded files.
    /// </summary>
    public string OutputPath { get; set; } = "./downloads";
}
