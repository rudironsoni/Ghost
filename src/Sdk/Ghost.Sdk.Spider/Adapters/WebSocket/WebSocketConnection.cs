using System.Net.WebSockets;
using System.Text;

namespace Ghost.Sdk.Spider.Adapters.WebSocket;

/// <summary>
/// Represents an active WebSocket connection with message handling capabilities.
/// </summary>
/// <remarks>
/// This class encapsulates a <see cref="ClientWebSocket"/> and provides high-level
/// methods for sending and receiving messages, managing connection state, and
/// handling reconnection logic.
/// </remarks>
public class WebSocketConnection : IDisposable
{
    private readonly ClientWebSocket _webSocket;
    private readonly WebSocketAdapterOptions _options;
    private readonly MessageBuffer _messageBuffer;
    private readonly object _lock = new();
    private readonly TimeProvider _timeProvider;
    private bool _disposed;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private readonly StringBuilder _jsonFragmentBuffer = new();

    /// <summary>
    /// Gets the unique identifier for this connection.
    /// </summary>
    /// <value>A GUID representing the connection ID.</value>
    public string ConnectionId { get; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Gets the URL of the WebSocket server.
    /// </summary>
    /// <value>The WebSocket server URL.</value>
    public string Url { get; }

    /// <summary>
    /// Gets a value indicating whether the connection is currently established.
    /// </summary>
    /// <value><c>true</c> if connected; otherwise, <c>false</c>.</value>
    public bool IsConnected => _webSocket.State == WebSocketState.Open;

    /// <summary>
    /// Gets a value indicating whether the connection is secure (WSS).
    /// </summary>
    /// <value><c>true</c> if using WSS; otherwise, <c>false</c>.</value>
    public bool IsSecure => Url.StartsWith("wss://", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the current state of the WebSocket.
    /// </summary>
    /// <value>The WebSocket state.</value>
    public WebSocketState State => _webSocket.State;

    /// <summary>
    /// Gets the close status when the connection is closed.
    /// </summary>
    /// <value>The WebSocket close status, or null if not closed.</value>
    public WebSocketCloseStatus? CloseStatus => _webSocket.CloseStatus;

    /// <summary>
    /// Gets the close status description.
    /// </summary>
    /// <value>The close status description.</value>
    public string CloseStatusDescription => _webSocket.CloseStatusDescription ?? string.Empty;

    /// <summary>
    /// Gets the number of reconnection attempts made.
    /// </summary>
    /// <value>The reconnection count.</value>
    public int ReconnectionCount { get; private set; }

    /// <summary>
    /// Gets the reason for disconnection.
    /// </summary>
    /// <value>The disconnect reason.</value>
    public string DisconnectReason { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the number of buffered messages.
    /// </summary>
    /// <value>The buffered message count.</value>
    public int BufferedMessageCount => _messageBuffer.Count;

    /// <summary>
    /// Gets the time when the connection was established.
    /// </summary>
    /// <value>The connection timestamp.</value>
    public DateTimeOffset ConnectedAt { get; private set; }

    /// <summary>
    /// Event raised when a message is received.
    /// </summary>
    public event EventHandler<WebSocketMessage>? MessageReceived;

    /// <summary>
    /// Event raised when the connection is closed.
    /// </summary>
    public event EventHandler<WebSocketCloseStatus>? ConnectionClosed;

    /// <summary>
    /// Event raised when an error occurs.
    /// </summary>
    public event EventHandler<Exception>? ErrorOccurred;

    /// <summary>
    /// Event raised when a reconnection attempt is made.
    /// </summary>
    public event EventHandler<int>? Reconnecting;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketConnection"/> class.
    /// </summary>
    /// <param name="url">The WebSocket server URL.</param>
    /// <param name="options">The adapter options.</param>
    /// <param name="timeProvider">Optional time provider for testability.</param>
    /// <exception cref="ArgumentNullException">Thrown when url or options is null.</exception>
    public WebSocketConnection(string url, WebSocketAdapterOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(options);
        Url = url;
        _options = options;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _webSocket = new ClientWebSocket();
        _messageBuffer = new MessageBuffer(options.BufferSize);

        ConfigureWebSocket();
    }

    private void ConfigureWebSocket()
    {
        _webSocket.Options.KeepAliveInterval = _options.Heartbeat.Enabled
            ? _options.Heartbeat.Interval
            : TimeSpan.Zero;

        // Add subprotocols if specified
        foreach (string subprotocol in _options.Subprotocols)
        {
            _webSocket.Options.AddSubProtocol(subprotocol);
        }
    }

    /// <summary>
    /// Establishes the WebSocket connection.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <exception cref="WebSocketException">Thrown when the connection fails.</exception>
    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.ConnectionTimeout);

        var uri = new Uri(Url);
        await _webSocket.ConnectAsync(uri, cts.Token).ConfigureAwait(false);

        ConnectedAt = _timeProvider.GetUtcNow();

        if (_options.Heartbeat.Enabled)
        {
            StartHeartbeat();
        }
    }

    /// <summary>
    /// Sends a text message.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        byte[] buffer = Encoding.UTF8.GetBytes(message);
        ArraySegment<byte> segment = new ArraySegment<byte>(buffer);

        await _webSocket.SendAsync(
            segment,
            WebSocketMessageType.Text,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends binary data.
    /// </summary>
    /// <param name="data">The binary data to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendAsync(byte[] data, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("WebSocket is not connected.");
        }

        var segment = new ArraySegment<byte>(data);

        await _webSocket.SendAsync(
            segment,
            WebSocketMessageType.Binary,
            endOfMessage: true,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Receives a message from the WebSocket.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The received WebSocket message.</returns>
    /// <exception cref="TimeoutException">Thrown when the receive operation times out.</exception>
    /// <exception cref="WebSocketException">Thrown when a WebSocket error occurs.</exception>
    public async Task<WebSocketMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_webSocket.State != WebSocketState.Open && _webSocket.State != WebSocketState.CloseReceived)
        {
            return null;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_options.ReceiveTimeout);

        byte[] buffer = new byte[_options.ReceiveBufferSize];
        StringBuilder messageBuilder = new StringBuilder();
        byte[]? binaryData = null;
        WebSocketMessageType messageType = WebSocketMessageType.Text;
        bool isComplete = false;

        try
        {
            while (!isComplete)
            {
                WebSocketReceiveResult result = await _webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    cts.Token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    DisconnectReason = "Server initiated close";
                    ConnectionClosed?.Invoke(this, result.CloseStatus ?? WebSocketCloseStatus.NormalClosure);
                    return new WebSocketMessage
                    {
                        MessageType = WebSocketMessageType.Close,
                        Content = result.CloseStatus?.ToString() ?? "Close",
                        IsComplete = true
                    };
                }

                messageType = result.MessageType;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string chunk = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    messageBuilder.Append(chunk);
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    if (binaryData == null)
                    {
                        binaryData = new byte[result.Count];
                        Buffer.BlockCopy(buffer, 0, binaryData, 0, result.Count);
                    }
                    else
                    {
                        byte[] newData = new byte[binaryData.Length + result.Count];
                        Buffer.BlockCopy(binaryData, 0, newData, 0, binaryData.Length);
                        Buffer.BlockCopy(buffer, 0, newData, binaryData.Length, result.Count);
                        binaryData = newData;
                    }
                }

                isComplete = result.EndOfMessage;
            }

            WebSocketMessage message;

            if (messageType == WebSocketMessageType.Text)
            {
                string content = messageBuilder.ToString();
                message = new WebSocketMessage(content, WebSocketMessageType.Text)
                {
                    IsComplete = true
                };

                // Handle JSON aggregation
                if (_options.AggregateJsonMessages && !IsValidJson(content))
                {
                    _jsonFragmentBuffer.Append(content);
                    if (IsValidJson(_jsonFragmentBuffer.ToString()))
                    {
                        message.Content = _jsonFragmentBuffer.ToString();
                        _jsonFragmentBuffer.Clear();
                    }
                    else
                    {
                        // Not complete yet, return null to continue waiting
                        return null;
                    }
                }
            }
            else
            {
                message = WebSocketMessage.CreateBinary(binaryData ?? Array.Empty<byte>());
                message.IsComplete = true;
            }

            if (_options.EnableMessageBuffering)
            {
                _messageBuffer.Add(message);
            }

            MessageReceived?.Invoke(this, message);

            return message;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("Receive operation timed out.");
        }
    }

    private static bool IsValidJson(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        content = content.Trim();

        // Quick check for JSON structure
        if ((content.StartsWith('{') && content.EndsWith('}')) ||
            (content.StartsWith('[') && content.EndsWith(']')))
        {
            try
            {
                System.Text.Json.JsonDocument.Parse(content);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    /// <summary>
    /// Closes the WebSocket connection gracefully.
    /// </summary>
    /// <param name="closeStatus">The close status.</param>
    /// <param name="statusDescription">The close status description.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CloseAsync(
        WebSocketCloseStatus closeStatus = WebSocketCloseStatus.NormalClosure,
        string statusDescription = "Closing",
        CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_webSocket.State == WebSocketState.Open || _webSocket.State == WebSocketState.CloseReceived)
        {
            try
            {
                await _webSocket.CloseAsync(
                    closeStatus,
                    statusDescription,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (WebSocketException)
            {
                // Ignore exceptions during close
            }
        }

        DisconnectReason = $"Client initiated close: {closeStatus}";
        StopHeartbeat();
    }

    /// <summary>
    /// Attempts to reconnect to the WebSocket server.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns><c>true</c> if reconnection succeeded; otherwise, <c>false</c>.</returns>
    public async Task<bool> ReconnectAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!_options.ReconnectionPolicy.Enabled)
        {
            return false;
        }

        ReconnectionPolicy policy = _options.ReconnectionPolicy;
        int attempt = 0;

        while (policy.MaxAttempts == -1 || attempt < policy.MaxAttempts)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            attempt++;
            ReconnectionCount++;

            Reconnecting?.Invoke(this, attempt);

            try
            {
                // Create a new WebSocket instance for reconnection
                _webSocket.Dispose();

                // Use reflection to set the private field
                System.Reflection.FieldInfo? field = typeof(WebSocketConnection).GetField(
                    "_webSocket",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);

                ClientWebSocket newSocket = new ClientWebSocket();
                field?.SetValue(this, newSocket);

                ConfigureWebSocket();

                await ConnectAsync(cancellationToken).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);

                if (policy.MaxAttempts != -1 && attempt >= policy.MaxAttempts)
                {
                    break;
                }

                TimeSpan delay = policy.CalculateDelay(attempt - 1);
                await Task.Delay(delay, _timeProvider, cancellationToken).ConfigureAwait(false);
            }
        }

        return false;
    }

    /// <summary>
    /// Flushes the message buffer and returns all buffered messages.
    /// </summary>
    /// <returns>An array of buffered messages.</returns>
    public WebSocketMessage[] FlushBuffer()
    {
        return _messageBuffer.Flush();
    }

    /// <summary>
    /// Clears the message buffer.
    /// </summary>
    public void ClearBuffer()
    {
        _messageBuffer.Clear();
    }

    private void StartHeartbeat()
    {
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = RunHeartbeatAsync(_heartbeatCts.Token);
    }

    private void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatTask = null;
        _heartbeatCts = null;
    }

    private async Task RunHeartbeatAsync(CancellationToken cancellationToken)
    {
        if (!_options.Heartbeat.Enabled)
            return;

        while (!cancellationToken.IsCancellationRequested && IsConnected)
        {
            try
            {
                await Task.Delay(_options.Heartbeat.Interval, _timeProvider, cancellationToken).ConfigureAwait(false);

                if (!IsConnected)
                    break;

                if (_options.Heartbeat.UseProtocolPing)
                {
                    // Protocol-level ping is handled by ClientWebSocket
                    continue;
                }

                // Send application-level heartbeat
                await SendAsync(_options.Heartbeat.Message, cancellationToken).ConfigureAwait(false);

                // Wait for pong response
                using CancellationTokenSource pongCts = new CancellationTokenSource(_options.Heartbeat.Timeout);
                WebSocketMessage? response = await ReceiveAsync(pongCts.Token).ConfigureAwait(false);

                if (response != null &&
                    _options.Heartbeat.ExpectedResponse != null &&
                    response.Content != _options.Heartbeat.ExpectedResponse)
                {
                    DisconnectReason = "Heartbeat response mismatch";
                    await CloseAsync(WebSocketCloseStatus.NormalClosure, "Heartbeat failed", CancellationToken.None).ConfigureAwait(false);
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex);
                DisconnectReason = $"Heartbeat error: {ex.Message}";
                break;
            }
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        StopHeartbeat();

        if (_webSocket.State == WebSocketState.Open ||
            _webSocket.State == WebSocketState.CloseReceived)
        {
            try
            {
                _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Disposing",
                    CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
            }
            catch
            {
                // Ignore exceptions during disposal
            }
        }

        _webSocket.Dispose();
        _messageBuffer.Clear();
        _jsonFragmentBuffer.Clear();

        GC.SuppressFinalize(this);
    }
}
