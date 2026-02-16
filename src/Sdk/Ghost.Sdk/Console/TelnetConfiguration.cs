namespace Ghost.Sdk.Console;

/// <summary>
/// Configuration for the telnet console debugging interface.
/// </summary>
public sealed class TelnetConfiguration
{
    /// <summary>
    /// Gets or sets whether the telnet console is enabled.
    /// Disabled by default for security.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Gets or sets the port to listen on.
    /// Default is 6023 (Scrapy default).
    /// </summary>
    public int Port { get; set; } = 6023;

    /// <summary>
    /// Gets or sets the bind address.
    /// Default is localhost only for security.
    /// </summary>
    public string BindAddress { get; set; } = "127.0.0.1";

    /// <summary>
    /// Gets or sets the username for authentication.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Gets or sets the password for authentication.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the list of allowed IP addresses or CIDR ranges.
    /// </summary>
    public List<string> AllowedIps { get; set; } = [];

    /// <summary>
    /// Gets or sets the session timeout duration.
    /// </summary>
    public TimeSpan SessionTimeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Gets or sets the maximum number of concurrent connections.
    /// </summary>
    public int MaxConnections { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether command history is enabled.
    /// </summary>
    public bool EnableCommandHistory { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum number of commands to keep in history.
    /// </summary>
    public int MaxHistorySize { get; set; } = 100;

    /// <summary>
    /// Gets or sets whether commands should be logged.
    /// </summary>
    public bool LogCommands { get; set; } = true;

    /// <summary>
    /// Gets or sets whether pause/resume commands are allowed.
    /// </summary>
    public bool AllowPauseResume { get; set; } = true;

    /// <summary>
    /// Gets or sets whether shutdown commands are allowed.
    /// </summary>
    public bool AllowShutdown { get; set; } = true;

    /// <summary>
    /// Gets or sets whether queue inspection is allowed.
    /// </summary>
    public bool AllowQueueInspection { get; set; } = true;

    /// <summary>
    /// Gets or sets whether stats inspection is allowed.
    /// </summary>
    public bool AllowStatsInspection { get; set; } = true;

    /// <summary>
    /// Gets or sets custom commands registered by extensions.
    /// </summary>
    public Dictionary<string, string> CustomCommands { get; set; } = [];
}
