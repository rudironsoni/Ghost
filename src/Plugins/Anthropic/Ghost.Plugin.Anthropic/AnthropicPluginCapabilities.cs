namespace Ghost.Plugin.Anthropic;

/// <summary>
/// Describes the capabilities of the Anthropic plugin.
/// </summary>
public sealed record AnthropicPluginCapabilities
{
    /// <summary>
    /// Gets whether the plugin requires a browser session.
    /// </summary>
    public bool RequiresBrowser { get; init; }

    /// <summary>
    /// Gets whether the plugin requires proxy support.
    /// </summary>
    public bool RequiresProxy { get; init; }

    /// <summary>
    /// Gets whether the plugin supports job operations.
    /// </summary>
    public bool SupportsJobs { get; init; }

    /// <summary>
    /// Gets whether the plugin supports social operations.
    /// </summary>
    public bool SupportsSocial { get; init; }

    /// <summary>
    /// Gets whether the plugin supports news operations.
    /// </summary>
    public bool SupportsNews { get; init; }

    /// <summary>
    /// Gets whether the plugin supports inference operations.
    /// </summary>
    public bool SupportsInference { get; init; } = true;
}
