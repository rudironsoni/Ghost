namespace Ghost.Plugin.Common;

/// <summary>
/// Interface for plugin readiness checks.
/// </summary>
public interface IPluginReadinessCheck
{
    /// <summary>
    /// Gets the name of the readiness check.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Performs the readiness check asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the readiness check result.</returns>
    Task<ReadinessCheckResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a readiness check.
/// </summary>
public sealed record ReadinessCheckResult(
    bool IsReady,
    string? Message = null);
