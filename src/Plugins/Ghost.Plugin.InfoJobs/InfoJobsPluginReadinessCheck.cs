using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.InfoJobs;

/// <summary>
/// Default implementation of InfoJobs plugin readiness check.
/// </summary>
internal sealed class InfoJobsPluginReadinessCheck : IInfoJobsPluginReadinessCheck
{
    private readonly ILogger<InfoJobsPluginReadinessCheck> _logger;
    private readonly IOptions<Ghost.Platform.InfoJobs.Jobs.InfoJobsOptions> _options;

    public InfoJobsPluginReadinessCheck(
        ILogger<InfoJobsPluginReadinessCheck> logger,
        IOptions<Ghost.Platform.InfoJobs.Jobs.InfoJobsOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public Task<bool> RunReadinessCheckAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        // Lightweight deterministic validation: check that required options exist or have defaults
        var options = _options.Value;

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
