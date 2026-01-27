using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghostwright.Contracts.Social;

/// <summary>
/// Abstraction for social platform clients (eg. Twitter/X, LinkedIn, Mastodon).
/// </summary>
public interface ISocialClient
{
    /// <summary>
    /// Platform name (eg. Twitter, LinkedIn).
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Gets a profile by id.
    /// </summary>
    Task<SocialProfile> GetProfileAsync(string profileId, CancellationToken ct = default);

    /// <summary>
    /// Searches profiles using provided criteria.
    /// </summary>
    Task<IReadOnlyList<SocialProfile>> SearchProfilesAsync(ProfileSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>
    /// Creates a post on the platform.
    /// </summary>
    Task<SocialPost> CreatePostAsync(CreatePostRequest request, CancellationToken ct = default);

    /// <summary>
    /// Gets the authenticated user's feed.
    /// </summary>
    Task<IReadOnlyList<SocialPost>> GetFeedAsync(FeedOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a direct message to a recipient.
    /// </summary>
    Task SendMessageAsync(string recipientId, string message, CancellationToken ct = default);

    /// <summary>
    /// Gets connections for the authenticated user or the supplied options.
    /// </summary>
    Task<IReadOnlyList<SocialConnection>> GetConnectionsAsync(ConnectionsOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Sends a connection/follow request to the given profile.
    /// </summary>
    Task SendConnectionRequestAsync(string profileId, string? message = null, CancellationToken ct = default);
}
