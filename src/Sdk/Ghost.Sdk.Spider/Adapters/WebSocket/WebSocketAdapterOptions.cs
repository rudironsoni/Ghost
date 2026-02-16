using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Adapters.WebSocket;

/// <summary>
/// Configuration options specific to the WebSocketAdapter.
/// </summary>
/// <remarks>
/// This class extends the base <see cref="AdapterOptions"/> with WebSocket-specific
/// configuration options for managing WebSocket connections, heartbeats, reconnection,
/// and message buffering.
/// </remarks>
public class WebSocketAdapterOptions : AdapterOptions
{
    /// <summary>
    /// Gets or sets the buffer size for WebSocket receive operations in bytes.
    /// </summary>
    /// <value>The receive buffer size. Defaults to 4KB.</value>
    /// <remarks>
    /// Larger buffers can handle larger messages but consume more memory.
    /// For most use cases, 4KB is sufficient as WebSocket frames are typically small.
    /// </remarks>
    public int ReceiveBufferSize { get; set; } = 4096;

    /// <summary>
    /// Gets or sets a value indicating whether to aggregate JSON message fragments.
    /// </summary>
    /// <value>
    /// <c>true</c> to automatically aggregate JSON fragments into complete messages;
    /// otherwise, <c>false</c>. Defaults to <c>true</c>.
    /// </value>
    /// <remarks>
    /// When enabled, the adapter will buffer incoming text messages and attempt to
    /// parse them as JSON, aggregating fragments until a complete JSON object is received.
    /// </remarks>
    public bool AggregateJsonMessages { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable message buffering.
    /// </summary>
    /// <value>
    /// <c>true</c> to buffer incoming messages; otherwise, <c>false</c>.
    /// Defaults to <c>false</c>.
    /// </value>
    public bool EnableMessageBuffering { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of messages to buffer.
    /// </summary>
    /// <value>The buffer capacity. Defaults to 100 messages.</value>
    /// <remarks>
    /// When the buffer reaches this capacity, older messages may be dropped
    /// depending on the buffer overflow policy.
    /// </remarks>
    public int BufferSize { get; set; } = 100;

    /// <summary>
    /// Gets or sets the heartbeat options for maintaining the connection.
    /// </summary>
    /// <value>The heartbeat configuration.</value>
    public HeartbeatOptions Heartbeat { get; set; } = new();

    /// <summary>
    /// Gets or sets the reconnection policy for handling connection failures.
    /// </summary>
    /// <value>The reconnection policy configuration.</value>
    public ReconnectionPolicy ReconnectionPolicy { get; set; } = new();

    /// <summary>
    /// Gets or sets a value indicating whether to automatically reconnect on disconnect.
    /// </summary>
    /// <value>
    /// <c>true</c> to automatically reconnect when the connection is lost;
    /// otherwise, <c>false</c>. Defaults to <c>false</c>.
    /// </value>
    public bool AutoReconnect { get; set; }

    /// <summary>
    /// Gets or sets the receive timeout for WebSocket operations.
    /// </summary>
    /// <value>The receive timeout. Defaults to 30 seconds.</value>
    public TimeSpan ReceiveTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the connection timeout for establishing WebSocket connections.
    /// </summary>
    /// <value>The connection timeout. Defaults to 10 seconds.</value>
    public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value indicating whether to use secure WebSocket (WSS) by default.
    /// </summary>
    /// <value>
    /// <c>true</c> to prefer WSS over WS; otherwise, <c>false</c>.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool UseSecureConnection { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to validate the server certificate.
    /// </summary>
    /// <value>
    /// <c>true</c> to validate SSL certificates; <c>false</c> to accept any certificate.
    /// Defaults to <c>true</c>.
    /// </value>
    public bool ValidateServerCertificate { get; set; } = true;

    /// <summary>
    /// Gets or sets custom subprotocols to request during connection.
    /// </summary>
    /// <value>A list of WebSocket subprotocol names.</value>
    public List<string> Subprotocols { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketAdapterOptions"/> class.
    /// </summary>
    public WebSocketAdapterOptions()
    {
        // WebSocket connections typically don't follow HTTP redirects
        FollowRedirects = false;
        // WebSocket connections don't use HTTP caching
        EnableCaching = false;
    }

    /// <inheritdoc/>
    public override void Validate()
    {
        base.Validate();

        if (ReceiveBufferSize <= 0)
        {
            throw new ArgumentException("ReceiveBufferSize must be greater than zero.", nameof(ReceiveBufferSize));
        }

        if (BufferSize <= 0)
        {
            throw new ArgumentException("BufferSize must be greater than zero.", nameof(BufferSize));
        }

        if (ReceiveTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("ReceiveTimeout must be greater than zero.", nameof(ReceiveTimeout));
        }

        if (ConnectionTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentException("ConnectionTimeout must be greater than zero.", nameof(ConnectionTimeout));
        }

        Heartbeat.Validate();
        ReconnectionPolicy.Validate();
    }

    /// <inheritdoc/>
    public override AdapterOptions Clone()
    {
        var clone = (WebSocketAdapterOptions)base.Clone();
        clone.Heartbeat = new HeartbeatOptions
        {
            Enabled = Heartbeat.Enabled,
            Interval = Heartbeat.Interval,
            Timeout = Heartbeat.Timeout,
            Message = Heartbeat.Message,
            ExpectedResponse = Heartbeat.ExpectedResponse,
            UseProtocolPing = Heartbeat.UseProtocolPing
        };
        clone.ReconnectionPolicy = new ReconnectionPolicy
        {
            Enabled = ReconnectionPolicy.Enabled,
            MaxAttempts = ReconnectionPolicy.MaxAttempts,
            InitialDelay = ReconnectionPolicy.InitialDelay,
            MaxDelay = ReconnectionPolicy.MaxDelay,
            BackoffMultiplier = ReconnectionPolicy.BackoffMultiplier,
            UseExponentialBackoff = ReconnectionPolicy.UseExponentialBackoff,
            UseJitter = ReconnectionPolicy.UseJitter,
            ReconnectOnNormalClose = ReconnectionPolicy.ReconnectOnNormalClose,
            ConnectionTimeout = ReconnectionPolicy.ConnectionTimeout
        };
        clone.Subprotocols = new List<string>(Subprotocols);
        return clone;
    }
}
