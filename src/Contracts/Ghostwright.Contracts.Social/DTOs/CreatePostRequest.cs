using System.Collections.Generic;

namespace Ghostwright.Contracts.Social;

/// <summary>
/// Request to create a new social post.
/// </summary>
public sealed record CreatePostRequest
{
    /// <summary>
    /// Content of the post.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// Optional media urls attached to the post.
    /// </summary>
    public IReadOnlyList<string> MediaUrls { get; init; } = System.Array.Empty<string>();
}
