using Microsoft.Extensions.Logging;

namespace Ghostwright.Platform.LinkedIn;

internal static partial class LinkedInLog
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse search node")]
    public static partial void LogFailedToParseSearchNode(ILogger logger, Exception ex);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Failed to parse job node")]
    public static partial void LogFailedToParseJobNode(ILogger logger, Exception ex);
}
