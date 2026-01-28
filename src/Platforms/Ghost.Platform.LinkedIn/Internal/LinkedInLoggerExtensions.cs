using System;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.LinkedIn.Internal;

internal static partial class LinkedInLoggerExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "LoginWithCookieAsync: cookie set but not logged in")]
    public static partial void LoginCookieSetNotLoggedIn(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "WarmUp failed")]
    public static partial void WarmUpFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "IsLoggedInAsync check failed")]
    public static partial void IsLoggedInCheckFailed(this ILogger logger, Exception exception);
}
