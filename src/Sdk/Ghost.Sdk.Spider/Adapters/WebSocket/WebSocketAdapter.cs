using System.Net.WebSockets;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Microsoft.Extensions.Logging;

namespace Ghost.Sdk.Spider.Adapters.WebSocket;

/// <summary>
/// Adapter for extracting content from WebSocket connections.
/// </summary>
/// <remarks>
/// This adapter provides WebSocket client functionality with support for:
/// <list type="bullet">
/// <item>Connection management and lifecycle</item>
/// <item>Automatic reconnection with configurable policies</item>
/// <item>Heartbeat/ping-pong for connection health monitoring</item>
/// <item>Message buffering and JSON aggregation</item>
/// <item>Both text and binary message handling</item>
/// </list>
/// </remarks>
public class WebSocketAdapter : IContentAdapter, IDisposable
{
    private readonly ILogger<WebSocketAdapter>? _logger;
    private readonly WebSocketAdapterOptions _defaultOptions;
    private readonly Dictionary<string, WebSocketConnection> _connections = [];
    private readonly object _connectionsLock = new();
    private bool _disposed;

    /// <inheritdoc/>
    public string Name => "WebSocket";

    /// <inheritdoc/>
    public ContentType ContentType => ContentType.WebSocket;

    /// <inheritdoc/>
    public bool IsAvailable => true;

    /// <summary>
    /// Event raised when a WebSocket message is received.
    /// </summary>
    public event EventHandler<WebSocketMessage>? MessageReceived;

    /// <summary>
    /// Event raised when a WebSocket connection is closed.
    /// </summary>
    public event EventHandler<string>? ConnectionClosed;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketAdapter"/> class.
    /// </summary>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public WebSocketAdapter(ILogger<WebSocketAdapter>? logger = null)
        : this(new WebSocketAdapterOptions(), logger)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketAdapter"/> class with custom options.
    /// </summary>
    /// <param name="options">The default adapter options.</param>
    /// <param name="logger">Optional logger for diagnostic information.</param>
    public WebSocketAdapter(WebSocketAdapterOptions options, ILogger<WebSocketAdapter>? logger = null)
    {
        _defaultOptions = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<bool> CanHandleAsync(Request request, CancellationToken cancellationToken = default)
    {
        if (request == null)
            return Task.FromResult(false);

        // Can handle WebSocket URLs
        if (!Uri.TryCreate(request.Url, UriKind.Absolute, out Uri? uri))
            return Task.FromResult(false);

        string scheme = uri.Scheme.ToLowerInvariant();
        bool canHandle = scheme is "ws" or "wss";

        // Check expected content type
        if (canHandle && request.ExpectedContentType != ContentType.Unknown)
        {
            canHandle = request.ExpectedContentType == ContentType.WebSocket;
        }

        return Task.FromResult(canHandle);
    }

    /// <inheritdoc/>
    public Task<Response> ExtractAsync(Request request, CancellationToken cancellationToken = default)
    {
        return ExtractAsync(request, _defaultOptions, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Response> ExtractAsync(
        Request request,
        AdapterOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(options);

        WebSocketAdapterOptions wsOptions = options as WebSocketAdapterOptions ?? _defaultOptions;
        DateTimeOffset startTime = DateTimeOffset.UtcNow;

        try
        {
            // Create or get existing connection
            WebSocketConnection connection = await GetOrCreateConnectionAsync(request.Url, wsOptions, cancellationToken)
                .ConfigureAwait(false);

            // Receive messages until timeout or close
            List<WebSocketMessage> messages = [];
            using CancellationTokenSource receiveCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            receiveCts.CancelAfter(wsOptions.ReceiveTimeout);

            try
            {
                while (!receiveCts.Token.IsCancellationRequested && connection.IsConnected)
                {
                    WebSocketMessage? message = await connection.ReceiveAsync(receiveCts.Token).ConfigureAwait(false);

                    if (message == null)
                        break;

                    if (message.IsClose)
                        break;

                    messages.Add(message);

                    // If we have enough messages, return them
                    if (messages.Count >= wsOptions.BufferSize)
                        break;
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                // Receive timeout - this is expected
            }

            // Build response content
            string content;
            if (messages.Count == 1)
            {
                content = messages[0].Content;
            }
            else if (messages.Count > 1)
            {
                content = System.Text.Json.JsonSerializer.Serialize(messages.Select(m => m.Content));
            }
            else
            {
                content = string.Empty;
            }

            var contentResult = new ContentResult
            {
                Content = content,
                ContentType = ContentType.WebSocket,
                MimeType = "application/websocket",
                Encoding = "utf-8",
                ContentLength = content.Length,
                ExtractedAt = DateTimeOffset.UtcNow,
                Success = true
            };

            var response = new Response(contentResult)
            {
                StatusCode = connection.IsConnected ? 101 : 200, // 101 = Switching Protocols
                ReasonPhrase = connection.IsConnected ? "WebSocket Connected" : "WebSocket Closed",
                FinalUrl = request.Url,
                AdapterName = Name,
                IsSuccess = true,
                RequestedAt = startTime,
                RespondedAt = DateTimeOffset.UtcNow
            };

            // Add metadata
            response.Metadata["ConnectionId"] = connection.ConnectionId;
            response.Metadata["MessageCount"] = messages.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            response.Metadata["IsConnected"] = connection.IsConnected.ToString();
            response.Metadata["ReconnectionCount"] = connection.ReconnectionCount.ToString(System.Globalization.CultureInfo.InvariantCulture);

            return response;
        }
        catch (WebSocketException ex)
        {
            return CreateErrorResponse($"WebSocket error: {ex.Message}", ex, startTime, request.Url);
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            return CreateErrorResponse("Request was canceled", ex, startTime, request.Url);
        }
        catch (TimeoutException ex)
        {
            return CreateErrorResponse($"Connection timeout after {wsOptions.ConnectionTimeout}", ex, startTime, request.Url);
        }
        catch (Exception ex)
        {
            return CreateErrorResponse($"Unexpected error: {ex.Message}", ex, startTime, request.Url);
        }
    }

    /// <summary>
    /// Connects to a WebSocket server and returns the connection.
    /// </summary>
    /// <param name="url">The WebSocket URL.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The WebSocket connection.</returns>
    public async Task<WebSocketConnection> ConnectAsync(string url, CancellationToken cancellationToken = default)
    {
        return await ConnectAsync(url, _defaultOptions, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Connects to a WebSocket server with custom options.
    /// </summary>
    /// <param name="url">The WebSocket URL.</param>
    /// <param name="options">The WebSocket options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The WebSocket connection.</returns>
    public async Task<WebSocketConnection> ConnectAsync(
        string url,
        WebSocketAdapterOptions options,
        CancellationToken cancellationToken = default)
    {
        return await GetOrCreateConnectionAsync(url, options, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Sends a message to a WebSocket connection.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="message">The message to send.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SendAsync(string connectionId, string message, CancellationToken cancellationToken = default)
    {
        WebSocketConnection? connection = GetConnection(connectionId)
            ?? throw new InvalidOperationException($"Connection {connectionId} not found.");

        await connection.SendAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Receives a message from a WebSocket connection.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The received message.</returns>
    public async Task<WebSocketMessage?> ReceiveAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        WebSocketConnection? connection = GetConnection(connectionId)
            ?? throw new InvalidOperationException($"Connection {connectionId} not found.");

        return await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Closes a WebSocket connection.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task CloseAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        WebSocketConnection? connection = GetConnection(connectionId);
        if (connection == null)
            return;

        await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", cancellationToken)
            .ConfigureAwait(false);

        lock (_connectionsLock)
        {
            _connections.Remove(connectionId);
        }
    }

    /// <summary>
    /// Gets a connection by ID.
    /// </summary>
    /// <param name="connectionId">The connection ID.</param>
    /// <returns>The WebSocket connection, or null if not found.</returns>
    public WebSocketConnection? GetConnection(string connectionId)
    {
        lock (_connectionsLock)
        {
            _connections.TryGetValue(connectionId, out WebSocketConnection? connection);
            return connection;
        }
    }

    /// <summary>
    /// Gets all active connections.
    /// </summary>
    /// <returns>A list of active connections.</returns>
    public IReadOnlyList<WebSocketConnection> GetActiveConnections()
    {
        lock (_connectionsLock)
        {
            return _connections.Values.Where(c => c.IsConnected).ToList();
        }
    }

    private async Task<WebSocketConnection> GetOrCreateConnectionAsync(
        string url,
        WebSocketAdapterOptions options,
        CancellationToken cancellationToken)
    {
        // Check for existing connection
        lock (_connectionsLock)
        {
            WebSocketConnection? existingConnection = _connections.Values.FirstOrDefault(c => c.Url == url && c.IsConnected);
            if (existingConnection != null)
            {
                return existingConnection;
            }
        }

        // Create new connection
        WebSocketConnection connection = new WebSocketConnection(url, options);

        // Wire up events
        connection.MessageReceived += (s, e) => MessageReceived?.Invoke(this, e);
        connection.ConnectionClosed += (s, e) =>
        {
            ConnectionClosed?.Invoke(this, url);

            lock (_connectionsLock)
            {
                _connections.Remove(connection.ConnectionId);
            }
        };

        // Connect
        await connection.ConnectAsync(cancellationToken).ConfigureAwait(false);

        // Store connection
        lock (_connectionsLock)
        {
            _connections[connection.ConnectionId] = connection;
        }

        return connection;
    }

    private Response CreateErrorResponse(string error, Exception? exception, DateTimeOffset startTime, string url)
    {
        ContentResult contentResult = ContentResult.CreateFailure(error, ContentType.WebSocket);

        return new Response(contentResult)
        {
            IsSuccess = false,
            Error = error,
            Exception = exception,
            AdapterName = Name,
            FinalUrl = url,
            RequestedAt = startTime,
            RespondedAt = DateTimeOffset.UtcNow
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        lock (_connectionsLock)
        {
            foreach (WebSocketConnection connection in _connections.Values)
            {
                try
                {
                    connection.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }

            _connections.Clear();
        }

        GC.SuppressFinalize(this);
    }
}
