using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.X;

public partial class XHealthCheckService
{
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Error, Message = "X platform health check failed: {Messages}")]
        public static partial void HealthCheckFailed(ILogger logger, string messages);

        [LoggerMessage(Level = LogLevel.Warning, Message = "X platform health check degraded: {Messages}")]
        public static partial void HealthCheckDegraded(ILogger logger, string messages);

        [LoggerMessage(Level = LogLevel.Information, Message = "X Metrics - Total: {Total}, Success: {SuccessRate:P}, RateLimits: {RateLimits}")]
        public static partial void Metrics(ILogger logger, long total, double successRate, long rateLimits);

        [LoggerMessage(Level = LogLevel.Error, Message = "Health check service error")]
        public static partial void HealthCheckServiceError(ILogger logger, Exception ex);
    }
}
