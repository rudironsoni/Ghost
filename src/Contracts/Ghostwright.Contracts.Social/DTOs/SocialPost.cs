using System;

namespace Ghostwright.Contracts.Social;

/// <summary>
/// Represents a post published on a social platform.
/// </summary>
public sealed record SocialPost
{
    /// <summary>
    /// Post identifier.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Author profile id.
    /// </summary>
    public string AuthorId { get; init; } = string.Empty;

    /// <summary>
    /// Content of the post.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// When the post was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Number of likes or similar positive reactions.
    /// </summary>
    public int Likes { get; init; }

    /// <summary>
    /// Number of shares/retweets.
    /// </summary>
    public int Shares { get; init; }
}
