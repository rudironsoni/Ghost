#pragma warning disable CA1848  // Use LoggerMessage delegates
#pragma warning disable CA1873  // Avoid expensive logging evaluation

using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.Common;

public abstract class PluginHealthCheckBase : IPluginReadinessCheck
{
    private readonly ILogger _logger;

    public string Name => GetType().Name;

    protected PluginHealthCheckBase(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReadinessCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Starting health check for {CheckName}", GetType().Name);

            ReadinessCheckResult result = await PerformCheckAsync(cancellationToken).ConfigureAwait(false);

            if (result.IsReady)
            {
                _logger.LogInformation("Health check passed for {CheckName}", GetType().Name);
            }
            else
            {
                _logger.LogWarning("Health check failed for {CheckName}: {Message}", GetType().Name, result.Message);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check threw exception for {CheckName}", GetType().Name);
            return new ReadinessCheckResult(false, $"Health check failed with exception: {ex.Message}");
        }
    }

    protected abstract Task<ReadinessCheckResult> PerformCheckAsync(CancellationToken cancellationToken);
}
