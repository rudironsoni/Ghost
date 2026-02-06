using Microsoft.Extensions.Logging;

namespace Ghost.Platform.X.Internal;

public partial class XThreadComposer
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Posting {Count} tweet(s) as thread")]
        public static partial void PostingThread(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Posting tweet {Index}/{Total}: {Preview}...")]
        public static partial void PostingTweet(ILogger logger, int index, int total, string preview);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Waiting {DelayMs}ms before next tweet")]
        public static partial void WaitingBeforeTweet(ILogger logger, int delayMs);

        [LoggerMessage(Level = LogLevel.Information, Message = "Successfully posted thread with {Count} tweets")]
        public static partial void ThreadPosted(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not extract tweet ID, using generated ID")]
        public static partial void TweetIdExtractionFailed(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not extract reply tweet ID, using generated ID")]
        public static partial void ReplyTweetIdExtractionFailed(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Uploading {Count} media files")]
        public static partial void UploadingMedia(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not find media input, attempting to click media button first")]
        public static partial void MediaInputNotFound(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Media file not found: {Path}")]
        public static partial void MediaFileNotFound(ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Unsupported media format: {Extension}")]
        public static partial void UnsupportedMediaFormat(ILogger logger, string extension);

        [LoggerMessage(Level = LogLevel.Warning, Message = "No valid media files to upload")]
        public static partial void NoValidMediaFiles(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Truncating to {Max} media files")]
        public static partial void TruncatingMediaFiles(ILogger logger, int max);

        [LoggerMessage(Level = LogLevel.Information, Message = "Setting {Count} media files for upload")]
        public static partial void SettingMediaFiles(ILogger logger, int count);

        [LoggerMessage(Level = LogLevel.Information, Message = "Media upload completed")]
        public static partial void MediaUploadCompleted(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to extract tweet ID from page")]
        public static partial void TweetIdFromPageExtractionFailed(ILogger logger, Exception ex);
    }
}
