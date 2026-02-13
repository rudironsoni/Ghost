namespace Ghost.Plugin.Glassdoor;

/// <summary>
/// Describes the capabilities of the Glassdoor plugin.
/// </summary>
public sealed record GlassdoorPluginCapabilities
{
    /// <summary>
    /// Gets whether the plugin requires a browser session.
    /// </summary>
    public bool RequiresBrowser { get; init; } = true;

    /// <summary>
    /// Gets whether the plugin requires proxy support.
    /// </summary>
    public bool RequiresProxy { get; init; }

    /// <summary>
    /// Gets whether the plugin supports job operations.
    /// </summary>
    public bool SupportsJobs { get; init; } = true;

    /// <summary>
    /// Gets whether the plugin supports social operations.
    /// </summary>
    public bool SupportsSocial { get; init; }

    /// <summary>
    /// Gets whether the plugin supports news operations.
    /// </summary>
    public bool SupportsNews { get; init; }
}
