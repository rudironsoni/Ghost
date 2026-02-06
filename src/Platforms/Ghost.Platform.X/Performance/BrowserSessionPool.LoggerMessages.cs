using Microsoft.Extensions.Logging;

namespace Ghost.Platform.X.Performance;

public partial class BrowserSessionPool
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Browser session pool initialized with max size {MaxSize}")]
        public static partial void PoolInitialized(ILogger logger, int maxSize);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Reused session from pool. Available: {Available}, InUse: {InUse}")]
        public static partial void SessionReused(ILogger logger, int available, int inUse);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Created new session. Total: {Total}")]
        public static partial void SessionCreated(ILogger logger, int total);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Returned session to pool. Available: {Available}, InUse: {InUse}")]
        public static partial void SessionReturned(ILogger logger, int available, int inUse);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Cleaned up idle session")]
        public static partial void IdleSessionCleaned(ILogger logger);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error during session cleanup")]
        public static partial void CleanupError(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Error disposing session")]
        public static partial void DisposeSessionError(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Information, Message = "Browser session pool disposed")]
        public static partial void PoolDisposed(ILogger logger);
    }
}
