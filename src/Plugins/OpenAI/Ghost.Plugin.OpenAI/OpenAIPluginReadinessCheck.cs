using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.OpenAI;

/// <summary>
/// Default implementation of OpenAI plugin readiness check.
/// </summary>
internal sealed class OpenAIPluginReadinessCheck : IOpenAIPluginReadinessCheck
{
    private readonly ILogger<OpenAIPluginReadinessCheck> _logger;
    private readonly IOptions<OpenAIOptions> _options;

    public OpenAIPluginReadinessCheck(
        ILogger<OpenAIPluginReadinessCheck> logger,
        IOptions<OpenAIOptions> options)
    {
        _logger = logger;
        _options = options;
    }

    /// <inheritdoc />
    public Task<bool> RunReadinessCheckAsync(System.Threading.CancellationToken cancellationToken = default)
    {
        // Lightweight deterministic validation: check that required options exist or have defaults
        OpenAIOptions options = _options.Value;

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
