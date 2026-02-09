namespace Ghost.Sdk.Console;

/// <summary>
/// Context provided to console commands for accessing spider state.
/// </summary>
public sealed class ConsoleContext
{
    /// <summary>
    /// Gets or sets the session start time.
    /// </summary>
    public DateTimeOffset SessionStart { get; set; }

    /// <summary>
    /// Gets or sets the authenticated username.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client IP address.
    /// </summary>
    public string ClientAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the configuration for command permissions.
    /// </summary>
    public TelnetConfiguration Configuration { get; set; } = new();
}
