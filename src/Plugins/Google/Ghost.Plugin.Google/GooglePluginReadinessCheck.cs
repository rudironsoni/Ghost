using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Google;

/// <summary>
/// Default implementation of Google plugin readiness check.
/// </summary>
internal sealed class GooglePluginReadinessCheck : IGooglePluginReadinessCheck
{
    private readonly ILogger<GooglePluginReadinessCheck> _logger;
    private readonly IOptions<Ghost.Plugin.Google.GoogleOptions> _options;

    public GooglePluginReadinessCheck(
        ILogger<GooglePluginReadinessCheck> logger,
        IOptions<Ghost.Plugin.Google.GoogleOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public Task<bool> RunReadinessCheckAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        // Lightweight deterministic validation: check that required options exist or have defaults
        GoogleOptions options = _options.Value;

        // Basic validation - ensure options are not null
        if (options == null)
        {
            return Task.FromResult(false);
        }

        // The plugin is considered ready if options are available
        // More detailed validation can be added as needed
        return Task.FromResult(true);
    }
}
