using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.X.MultiAccount;

public partial class XAccountManager
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Information, Message = "Registered X account: {AccountId}")]
        public static partial void AccountRegistered(ILogger logger, string accountId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "No X accounts registered")]
        public static partial void NoAccountsRegistered(ILogger logger);

        [LoggerMessage(Level = LogLevel.Debug, Message = "Selected X account: {AccountId}")]
        public static partial void AccountSelected(ILogger logger, string accountId);

        [LoggerMessage(Level = LogLevel.Warning, Message = "All X accounts are rate limited or disabled")]
        public static partial void AllAccountsRateLimited(ILogger logger);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Marked X account {AccountId} as rate limited for {Duration}")]
        public static partial void AccountMarkedRateLimited(ILogger logger, string accountId, TimeSpan duration);
    }
}
