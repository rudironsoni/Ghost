using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.X.Internal;

public partial class XAuthenticator
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "User appears to be logged in to X")]
        public static partial void UserLoggedIn(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "User appears to be logged out of X")]
        public static partial void UserLoggedOut(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Could not determine login state, assuming not logged in")]
        public static partial void LoginStateUndetermined(ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error checking login state")]
        public static partial void LoginStateCheckError(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Debug, Message = "User is already authenticated")]
        public static partial void AlreadyAuthenticated(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Loading storage state from {Path}")]
        public static partial void LoadingStorageState(ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Information, Message = "Successfully authenticated using storage state")]
        public static partial void AuthenticationSuccessful(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to load storage state from {Path}")]
        public static partial void StorageStateLoadFailed(ILogger logger, Exception ex, string path);

        [LoggerMessage(Level = LogLevel.Error, Message = "User is not authenticated and no valid storage state found")]
        public static partial void NotAuthenticated(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Warming up X session")]
        public static partial void WarmingUp(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Warm-up complete. Logged in: {IsLoggedIn}")]
        public static partial void WarmUpComplete(ILogger logger, bool isLoggedIn);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Warm-up failed, but continuing anyway")]
        public static partial void WarmUpFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "No storage state path configured, cannot save authentication")]
        public static partial void NoStorageStatePath(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "Authentication state saved to {Path}")]
        public static partial void AuthenticationStateSaved(ILogger logger, string path);

        [LoggerMessage(Level = LogLevel.Error, Message = "Failed to save authentication state to {Path}")]
        public static partial void AuthenticationStateSaveFailed(ILogger logger, Exception ex, string path);
    }
}
