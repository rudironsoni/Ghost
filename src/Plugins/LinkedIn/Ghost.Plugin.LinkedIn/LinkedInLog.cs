using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.LinkedIn;

internal static partial class LinkedInLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse search node")]
    public static partial void LogFailedToParseSearchNode(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse job node")]
    public static partial void LogFailedToParseJobNode(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn job details fetch failed for job {JobId}. Returning mock job data as fallback.")]
    public static partial void LogJobDetailsFetchFailed(ILogger logger, string jobId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn news articles fetch failed. Returning mock news data as fallback.")]
    public static partial void LogNewsArticlesFetchFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn news search failed. Returning mock news data as fallback.")]
    public static partial void LogNewsSearchFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn profile fetch failed for {ProfileId}. Returning mock profile data as fallback.")]
    public static partial void LogProfileFetchFailed(ILogger logger, string profileId, Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to dispose page in {Operation}")]
    public static partial void LogPageDisposeFailed(ILogger logger, string operation, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse feed post content - element may be stale or detached")]
    public static partial void LogFeedPostParseFailed(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse connection card - element may be stale or detached")]
    public static partial void LogConnectionCardParseFailed(ILogger logger, Exception ex);
}
