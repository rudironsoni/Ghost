using Microsoft.Extensions.Logging;

namespace Ghost.Platform.X;

public partial class XSocialClient
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Fetching X profile for: {ProfileId}")]
        public static partial void FetchingProfile(ILogger logger, string profileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Successfully fetched profile for: {ProfileId}")]
        public static partial void ProfileFetched(ILogger logger, string profileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Searching X profiles for: {Query}")]
        public static partial void SearchingProfiles(ILogger logger, string? query);

        [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} profiles for query: {Query}")]
        public static partial void ProfilesFound(ILogger logger, int count, string? query);

        [LoggerMessage(Level = LogLevel.Information, Message = "Creating X post with content length: {Length}")]
        public static partial void CreatingPost(ILogger logger, int length);

        [LoggerMessage(Level = LogLevel.Information, Message = "Successfully created X post with ID: {PostId}")]
        public static partial void PostCreated(ILogger logger, string postId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Fetching X feed")]
        public static partial void FetchingFeed(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Fetched {Count} posts from feed")]
        public static partial void FeedFetched(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Information, Message = "Sending message to: {RecipientId}")]
        public static partial void SendingMessage(ILogger logger, string recipientId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Successfully sent message to: {RecipientId}")]
        public static partial void MessageSent(ILogger logger, string recipientId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Fetching connections for: {ProfileId}")]
        public static partial void FetchingConnections(ILogger logger, string profileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Fetched {Count} connections for: {ProfileId}")]
        public static partial void ConnectionsFetched(ILogger logger, int count, string profileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Following user: {ProfileId}")]
        public static partial void FollowingUser(ILogger logger, string profileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Already following user: {ProfileId}")]
        public static partial void AlreadyFollowing(ILogger logger, string profileId);

        [LoggerMessage(Level = LogLevel.Information, Message = "Successfully followed user: {ProfileId}")]
        public static partial void UserFollowed(ILogger logger, string profileId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract profile name")]
        public static partial void ProfileNameExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract profile bio")]
        public static partial void ProfileBioExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract follower count")]
        public static partial void FollowerCountExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract search result")]
        public static partial void SearchResultExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to extract search results")]
        public static partial void SearchResultsExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract feed post")]
        public static partial void FeedPostExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to extract feed posts")]
        public static partial void FeedPostsExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract post from article")]
        public static partial void PostFromArticleExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract connection")]
        public static partial void ConnectionExtractionFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to extract connections")]
        public static partial void ConnectionsExtractionFailed(ILogger logger, Exception ex);
    }
}
