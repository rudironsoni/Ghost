using System;
using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.LinkedIn.Internal;

internal static partial class LinkedInLoggerExtensions
{
    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "LoginWithCookieAsync: cookie set but not logged in")]
    public static partial void LoginCookieSetNotLoggedIn(this ILogger logger);

    [LoggerMessage(EventId = 2, Level = LogLevel.Debug, Message = "WarmUp failed")]
    public static partial void WarmUpFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "IsLoggedInAsync check failed")]
    public static partial void IsLoggedInCheckFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "LinkedInSocialClient.GetProfileAsync: not logged in - scraping may be limited")]
    public static partial void LogNotLoggedIn(this ILogger logger);

    [LoggerMessage(EventId = 5, Level = LogLevel.Debug, Message = "Failed to verify LinkedIn login state")]
    public static partial void LogLoginVerificationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 6, Level = LogLevel.Debug, Message = "Failed to parse experience section")]
    public static partial void LogExperienceParseFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 7, Level = LogLevel.Debug, Message = "Failed to parse education section")]
    public static partial void LogEducationParseFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 8, Level = LogLevel.Debug, Message = "Failed to parse an experience item")]
    public static partial void LogExperienceItemParseFailed(this ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9, Level = LogLevel.Debug, Message = "Failed to parse an education item")]
    public static partial void LogEducationItemParseFailed(this ILogger logger, Exception exception);
}
