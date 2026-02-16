namespace Ghost.Sdk.Console;

/// <summary>
/// Represents an active telnet console session.
/// </summary>
public sealed class TelnetSession
{
    /// <summary>
    /// Gets or sets the unique session identifier.
    /// </summary>
    public string SessionId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets or sets when the session was established.
    /// </summary>
    public DateTimeOffset ConnectedAt { get; set; }

    /// <summary>
    /// Gets or sets when the last command was executed.
    /// </summary>
    public DateTimeOffset LastActivity { get; set; }

    /// <summary>
    /// Gets or sets the client IP address.
    /// </summary>
    public string ClientAddress { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the session has been authenticated.
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Gets or sets the command history for this session.
    /// </summary>
    public List<string> CommandHistory { get; set; } = [];
}
