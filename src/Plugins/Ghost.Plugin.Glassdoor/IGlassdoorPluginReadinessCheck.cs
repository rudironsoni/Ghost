using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Plugin.Glassdoor;

/// <summary>
/// Interface for Glassdoor plugin readiness check functionality.
/// </summary>
public interface IGlassdoorPluginReadinessCheck
{
    /// <summary>
    /// Runs a readiness check to verify plugin configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the readiness check passes, false otherwise.</returns>
    Task<bool> RunReadinessCheckAsync(CancellationToken cancellationToken = default);
}
