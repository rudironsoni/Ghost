using System;
using System.Collections.Generic;

namespace Ghost.Contracts.Social;

/// <summary>
/// Represents a user's profile on a social platform.
/// </summary>
public sealed record SocialProfile
{
    /// <summary>
    /// Unique identifier for the profile on the platform.
    /// </summary>
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// Display name of the profile.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// The user's handle or username.
    /// </summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>
    /// Short biography or description provided by the user.
    /// </summary>
    public string? Bio { get; init; }

    /// <summary>
    /// Number of followers.
    /// </summary>
    public int FollowersCount { get; init; }
    /// <summary>
    /// Number of profiles this user is following.
    /// </summary>
    public int FollowingCount { get; init; }

    /// <summary>
    /// When the profile was created.
    /// </summary>
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Work experience entries for the profile.
    /// </summary>
    public List<SocialExperience> Experience { get; init; } = [];

    /// <summary>
    /// Education entries for the profile.
    /// </summary>
    public List<SocialEducation> Education { get; init; } = [];
}
