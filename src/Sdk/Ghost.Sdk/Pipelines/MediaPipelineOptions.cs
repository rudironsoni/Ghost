using System.Collections.Generic;

namespace Ghost.Sdk.Pipelines;

/// <summary>
/// Configuration options for the media pipeline.
/// </summary>
public class MediaPipelineOptions
{
    /// <summary>
    /// Gets or sets the maximum file size in bytes.
    /// Default is 100MB.
    /// </summary>
    public long MaxFileSize { get; set; } = 100 * 1024 * 1024;

    /// <summary>
    /// Gets or sets the list of allowed file extensions.
    /// Empty list allows all extensions.
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = new();

    /// <summary>
    /// Gets or sets whether to calculate checksums for downloaded files.
    /// Default is true.
    /// </summary>
    public bool CalculateChecksum { get; set; } = true;
}
