using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.LinkedIn;

/// <summary>
/// Default implementation of LinkedIn plugin readiness check.
/// </summary>
internal sealed class LinkedInPluginReadinessCheck : ILinkedInPluginReadinessCheck
{
    private readonly ILogger<LinkedInPluginReadinessCheck> _logger;
    private readonly IOptions<Ghost.Plugin.LinkedIn.LinkedInOptions> _options;

    public LinkedInPluginReadinessCheck(
        ILogger<LinkedInPluginReadinessCheck> logger,
        IOptions<Ghost.Plugin.LinkedIn.LinkedInOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public Task<bool> RunReadinessCheckAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        // Lightweight deterministic validation: check that required options exist or have defaults
        LinkedInOptions options = _options.Value;

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
