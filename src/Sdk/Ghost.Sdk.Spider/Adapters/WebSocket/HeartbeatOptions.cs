namespace Ghost.Sdk.Spider.Adapters.WebSocket;

/// <summary>
/// Defines options for WebSocket heartbeat (ping-pong) mechanism.
/// </summary>
/// <remarks>
/// Heartbeat mechanisms help maintain WebSocket connections by sending periodic
/// ping messages and expecting pong responses. This can detect broken connections
/// and keep NAT gateways or proxies from timing out idle connections.
/// </remarks>
public class HeartbeatOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether heartbeat is enabled.
    /// </summary>
    /// <value>
    /// <c>true</c> to enable automatic heartbeat messages; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the interval between heartbeat messages.
    /// </summary>
    /// <value>
    /// The time between sending heartbeat messages. Defaults to 30 seconds.
    /// </value>
    /// <remarks>
    /// A shorter interval provides faster detection of broken connections but
    /// increases network traffic. A typical range is 15-60 seconds.
    /// </remarks>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout for heartbeat responses.
    /// </summary>
    /// <value>
    /// The maximum time to wait for a pong response after sending a ping.
    /// Defaults to 10 seconds.
    /// </value>
    /// <remarks>
    /// If no pong is received within this timeout, the connection is considered
    /// broken and may trigger a reconnection attempt.
    /// </remarks>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets the heartbeat message payload.
    /// </summary>
    /// <value>
    /// The message to send as heartbeat. Defaults to "ping".
    /// </value>
    /// <remarks>
    /// Some servers expect a specific heartbeat message format or JSON payload.
    /// Customize this property to match server requirements.
    /// </remarks>
    public string Message { get; set; } = "ping";

    /// <summary>
    /// Gets or sets the expected heartbeat response.
    /// </summary>
    /// <value>
    /// The expected response message. Defaults to "pong".
    /// </value>
    /// <remarks>
    /// If the server responds with a different message, set this to match.
    /// Leave null to accept any response as valid.
    /// </remarks>
    public string? ExpectedResponse { get; set; } = "pong";

    /// <summary>
    /// Gets or sets a value indicating whether to use WebSocket ping frames.
    /// </summary>
    /// <value>
    /// <c>true</c> to use WebSocket protocol ping/pong frames; <c>false</c> to use
    /// application-level messages. Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// WebSocket protocol-level ping/pong frames are handled automatically by the
    /// WebSocket implementation and are generally more efficient. However, some
    /// servers require application-level heartbeat messages.
    /// </remarks>
    public bool UseProtocolPing { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="HeartbeatOptions"/> class.
    /// </summary>
    public HeartbeatOptions()
    {
    }

    /// <summary>
    /// Creates a disabled heartbeat configuration.
    /// </summary>
    /// <returns>A new <see cref="HeartbeatOptions"/> instance with heartbeat disabled.</returns>
    public static HeartbeatOptions Disabled()
    {
        return new HeartbeatOptions { Enabled = false };
    }

    /// <summary>
    /// Creates a default heartbeat configuration.
    /// </summary>
    /// <returns>A new <see cref="HeartbeatOptions"/> instance with default settings.</returns>
    public static HeartbeatOptions Default()
    {
        return new HeartbeatOptions();
    }

    /// <summary>
    /// Validates the heartbeat options.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when configuration values are invalid.</exception>
    public void Validate()
    {
        if (Enabled)
        {
            if (Interval <= TimeSpan.Zero)
            {
                throw new ArgumentException("Heartbeat interval must be greater than zero.", nameof(Interval));
            }

            if (Timeout <= TimeSpan.Zero)
            {
                throw new ArgumentException("Heartbeat timeout must be greater than zero.", nameof(Timeout));
            }

            if (Timeout >= Interval)
            {
                throw new ArgumentException("Heartbeat timeout must be less than interval.", nameof(Timeout));
            }

            if (!UseProtocolPing && string.IsNullOrWhiteSpace(Message))
            {
                throw new ArgumentException("Heartbeat message cannot be null or whitespace when not using protocol ping.", nameof(Message));
            }
        }
    }
}
