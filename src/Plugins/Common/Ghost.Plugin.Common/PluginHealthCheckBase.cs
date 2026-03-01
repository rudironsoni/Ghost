using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.Common;

public abstract partial class PluginHealthCheckBase : IPluginReadinessCheck
{
    private readonly ILogger _logger;

    public string Name => GetType().Name;

    protected PluginHealthCheckBase(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
    }

    // LoggerMessage source generators (EventIds 5000-5099 for Plugins)
    [LoggerMessage(EventId = 5000, Level = LogLevel.Debug, Message = "Starting health check for {CheckName}")]
    private static partial void LogHealthCheckStarting(ILogger logger, string checkName);

    [LoggerMessage(EventId = 5001, Level = LogLevel.Information, Message = "Health check passed for {CheckName}")]
    private static partial void LogHealthCheckPassed(ILogger logger, string checkName);

    [LoggerMessage(EventId = 5002, Level = LogLevel.Warning, Message = "Health check failed for {CheckName}: {Message}")]
    private static partial void LogHealthCheckFailed(ILogger logger, string checkName, string message);

    [LoggerMessage(EventId = 5003, Level = LogLevel.Error, Message = "Health check threw exception for {CheckName}")]
    private static partial void LogHealthCheckException(ILogger logger, Exception ex, string checkName);

    public async Task<ReadinessCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            string checkName = GetType().Name;
            LogHealthCheckStarting(_logger, checkName);

            ReadinessCheckResult result = await PerformCheckAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsReady)
            {
                LogHealthCheckPassed(_logger, checkName);
            }
            else
            {
                LogHealthCheckFailed(_logger, checkName, result.Message ?? "Unknown error");
            }

            return result;
        }
        catch (Exception ex)
        {
            LogHealthCheckException(_logger, ex, GetType().Name);
            return new ReadinessCheckResult(false, $"Health check failed with exception: {ex.Message}");
        }
    }

    protected abstract Task<ReadinessCheckResult> PerformCheckAsync(CancellationToken cancellationToken);
}
