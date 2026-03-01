using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Plugin.Google;

/// <summary>
/// Interface for Google plugin readiness check functionality.
/// </summary>
public interface IGooglePluginReadinessCheck
{
    /// <summary>
    /// Runs a readiness check to verify plugin configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the readiness check passes, false otherwise.</returns>
    public Task<bool> RunReadinessCheckAsync(CancellationToken cancellationToken = default);
}
