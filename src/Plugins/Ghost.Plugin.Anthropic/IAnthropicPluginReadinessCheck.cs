using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Plugin.Anthropic;

/// <summary>
/// Interface for Anthropic plugin readiness check functionality.
/// </summary>
public interface IAnthropicPluginReadinessCheck
{
    /// <summary>
    /// Runs a readiness check to verify plugin configuration.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the readiness check passes, false otherwise.</returns>
    Task<bool> RunReadinessCheckAsync(CancellationToken cancellationToken = default);
}
