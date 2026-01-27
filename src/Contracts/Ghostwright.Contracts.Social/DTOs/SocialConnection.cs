using System;

namespace Ghostwright.Contracts.Social;

/// <summary>
/// Represents a relationship or connection between two profiles.
/// </summary>
public sealed record SocialConnection
{
    /// <summary>
    /// Unique id for the connection.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Source profile id (who initiated the connection).
    /// </summary>
    public string FromProfileId { get; init; } = string.Empty;

    /// <summary>
    /// Target profile id.
    /// </summary>
    public string ToProfileId { get; init; } = string.Empty;

    /// <summary>
    /// When the connection was established.
    /// </summary>
    public DateTimeOffset ConnectedAt { get; init; } = DateTimeOffset.UtcNow;
}
