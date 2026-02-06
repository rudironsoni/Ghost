using Microsoft.Extensions.Logging;

namespace Ghost.Platform.X.Configuration;

public partial class XConfigurationValidator
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "StorageStatePath is not configured. Authentication will fail. Please authenticate and save storage state to use X platform.")]
        public static partial void StorageStatePathNotConfigured(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "PageLoadTimeout is set to {Timeout}s which may be too short for slow connections")]
        public static partial void PageLoadTimeoutTooShort(ILogger logger, int timeout);

        [LoggerMessage(Level = LogLevel.Warning, Message = "MaxImageSizeMB is set to {Size}MB which exceeds X's limit of 5MB")]
        public static partial void MaxImageSizeExceeded(ILogger logger, int size);

        [LoggerMessage(Level = LogLevel.Warning, Message = "MaxVideoSizeMB is set to {Size}MB which exceeds X's limit of 512MB")]
        public static partial void MaxVideoSizeExceeded(ILogger logger, int size);

        [LoggerMessage(Level = LogLevel.Information, Message = "X platform configuration validated successfully")]
        public static partial void ConfigurationValidated(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Storage state file '{Path}' is valid JSON")]
        public static partial void StorageStateFileValid(ILogger logger, string path);
    }
}

public partial class XPlatformHealthCheck
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Debug, Message = "Starting X platform health check")]
        public static partial void StartingHealthCheck(ILogger logger);

        [LoggerMessage(Level = LogLevel.Information, Message = "X platform health check passed")]
        public static partial void HealthCheckPassed(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "X platform health check returned status: {Status}")]
        public static partial void HealthCheckStatus(ILogger logger, HealthStatus status);

        [LoggerMessage(Level = LogLevel.Error, Message = "Health check failed with exception")]
        public static partial void HealthCheckException(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Browser connectivity check failed")]
        public static partial void BrowserConnectivityFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Authentication state file is readable")]
        public static partial void AuthStateFileReadable(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Authentication state check failed")]
        public static partial void AuthStateCheckFailed(ILogger logger, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "X platform connectivity check failed")]
        public static partial void XConnectivityCheckFailed(ILogger logger, Exception ex);
    }
}
