using Microsoft.Extensions.Logging;

namespace Ghostwright.Platform.LinkedIn.Internal;

internal static partial class LinkedInLogGuest
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn guest API returned 429 - throttled")]
    public static partial void LogGuestApiThrottled(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "LinkedIn guest job endpoint returned 429 for {JobId}")]
    public static partial void LogGuestJobEndpointThrottled(ILogger logger, string jobId);
}
