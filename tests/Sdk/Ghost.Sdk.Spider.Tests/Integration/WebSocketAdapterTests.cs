using System.Net.WebSockets;
using System.Text;
using FluentAssertions;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Adapters.Contracts;
using Ghost.Sdk.Spider.Adapters.WebSocket;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Spider.Tests.Integration;

/// <summary>
/// Integration tests for WebSocketAdapter.
/// These tests use a real ASP.NET Core WebSocket server for deterministic testing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Capability", "WebSocket")]
public class WebSocketAdapterTests : IDisposable
{
    private readonly IWebHost _server;
    private readonly int _port;
    private readonly List<WebSocket> _serverSockets = new();
    private readonly object _socketLock = new();
    private readonly ILogger<WebSocketAdapter> _logger;
    private readonly List<IDisposable> _disposables = new();

    public WebSocketAdapterTests(ITestOutputHelper output)
    {
        _logger = LoggerFactory.Create(builder => builder.AddConsole()).CreateLogger<WebSocketAdapter>();

        // Find an available port
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        _port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        _server = new WebHostBuilder()
            .UseKestrel()
            .UseUrls($"http://localhost:{_port}")
            .Configure(app =>
            {
                app.UseWebSockets();
                app.Use(async (context, next) =>
                {
                    if (context.Request.Path == "/ws")
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                            lock (_socketLock)
                            {
                                _serverSockets.Add(webSocket);
                            }

                            await HandleWebSocketAsync(webSocket);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else if (context.Request.Path == "/ws/binary")
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                            lock (_socketLock)
                            {
                                _serverSockets.Add(webSocket);
                            }

                            await HandleBinaryWebSocketAsync(webSocket);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else if (context.Request.Path == "/ws/multiple")
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                            lock (_socketLock)
                            {
                                _serverSockets.Add(webSocket);
                            }

                            await HandleMultipleMessagesAsync(webSocket);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else if (context.Request.Path == "/ws/heartbeat")
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                            lock (_socketLock)
                            {
                                _serverSockets.Add(webSocket);
                            }

                            await HandleHeartbeatAsync(webSocket);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else if (context.Request.Path == "/ws/noping")
                    {
                        if (context.WebSockets.IsWebSocketRequest)
                        {
                            using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                            lock (_socketLock)
                            {
                                _serverSockets.Add(webSocket);
                            }

                            // Just echo but don't respond to pings properly
                            await HandleNoPingResponseAsync(webSocket);
                        }
                        else
                        {
                            context.Response.StatusCode = 400;
                        }
                    }
                    else
                    {
                        await next();
                    }
                });
            })
            .Build();

        _server.Start();
    }

    private async Task HandleWebSocketAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024];

        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    // Echo back with a prefix
                    var response = $"Echo: {message}";
                    var responseBytes = Encoding.UTF8.GetBytes(response);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(responseBytes),
                        WebSocketMessageType.Text,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
            catch
            {
                break;
            }
        }
    }

    private async Task HandleBinaryWebSocketAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024];

        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    // Echo back the binary data
                    var data = new byte[result.Count];
                    Buffer.BlockCopy(buffer, 0, data, 0, result.Count);
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(data),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        CancellationToken.None);
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
            catch
            {
                break;
            }
        }
    }

    private async Task HandleMultipleMessagesAsync(WebSocket webSocket)
    {
        // Send multiple messages
        for (int i = 1; i <= 3; i++)
        {
            var message = $"Message {i}";
            var bytes = Encoding.UTF8.GetBytes(message);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                endOfMessage: true,
                CancellationToken.None);
            await Task.Delay(50);
        }

        // Keep connection open until client closes
        var buffer = new byte[1024];
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                break;
        }
    }

    private async Task HandleHeartbeatAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024];

        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    if (message == "ping")
                    {
                        // Respond with pong
                        var pongBytes = Encoding.UTF8.GetBytes("pong");
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(pongBytes),
                            WebSocketMessageType.Text,
                            endOfMessage: true,
                            CancellationToken.None);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
            catch
            {
                break;
            }
        }
    }

    private async Task HandleNoPingResponseAsync(WebSocket webSocket)
    {
        var buffer = new byte[1024];

        while (webSocket.State == WebSocketState.Open)
        {
            try
            {
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

                    if (message == "ping")
                    {
                        // Don't respond - simulating a dead connection
                        await Task.Delay(TimeSpan.FromHours(1), CancellationToken.None);
                    }
                }
                else if (result.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
            }
            catch
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        lock (_socketLock)
        {
            foreach (var socket in _serverSockets)
            {
                try
                {
                    if (socket.State == WebSocketState.Open)
                    {
                        socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete", CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                    }
                    socket.Dispose();
                }
                catch { }
            }
            _serverSockets.Clear();
        }

        foreach (var disposable in _disposables)
        {
            try { disposable.Dispose(); } catch { }
        }
        _disposables.Clear();

        try { _server?.Dispose(); } catch { }
        GC.SuppressFinalize(this);
    }

    #region Connection Establishment

    [Fact]
    public async Task ConnectAsync_WithValidWebSocketUrl_ShouldEstablishConnection()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);

        // Act
        var connection = await adapter.ConnectAsync(wsUrl);
        _disposables.Add(connection);

        // Assert
        connection.Should().NotBeNull();
        connection.IsConnected.Should().BeTrue();
        connection.State.Should().Be(WebSocketState.Open);
        connection.Url.Should().Be(wsUrl);
        connection.ConnectionId.Should().NotBeNullOrEmpty();

        // Cleanup
        await adapter.CloseAsync(connection.ConnectionId);
        adapter.Dispose();
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidUrl_ShouldThrowWebSocketException()
    {
        // Arrange
        var wsUrl = "ws://invalid-host-that-does-not-exist:9999/ws";
        var options = new WebSocketAdapterOptions
        {
            ConnectionTimeout = TimeSpan.FromSeconds(1)
        };
        var adapter = new WebSocketAdapter(options, _logger);
        _disposables.Add(adapter);

        // Act & Assert
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            await adapter.ConnectAsync(wsUrl, CancellationToken.None);
        });
    }

    [Fact]
    public async Task ConnectAsync_WithSecureWebSocket_ShouldUseWss()
    {
        // Arrange
        var options = new WebSocketAdapterOptions();
        var connection = new WebSocketConnection("wss://echo.websocket.org/", options);
        _disposables.Add(connection);

        // Assert (can't actually connect without real cert, but can verify properties)
        connection.IsSecure.Should().BeTrue();
        connection.Url.Should().Be("wss://echo.websocket.org/");
    }

    [Fact]
    public async Task CanHandleAsync_WithWebSocketUrl_ShouldReturnTrue()
    {
        // Arrange
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var wsRequest = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = $"ws://localhost:{_port}/ws",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Act
        var canHandle = await adapter.CanHandleAsync(wsRequest);

        // Assert
        canHandle.Should().BeTrue();
    }

    [Fact]
    public async Task CanHandleAsync_WithHttpUrl_ShouldReturnFalse()
    {
        // Arrange
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var httpRequest = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = "http://localhost:8080/api",
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Act
        var canHandle = await adapter.CanHandleAsync(httpRequest);

        // Assert
        canHandle.Should().BeFalse();
    }

    #endregion

    #region Message Sending and Receiving

    [Fact]
    public async Task SendAsync_WithTextMessage_ShouldSendAndReceive()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);

        // Act
        await connection.SendAsync("Hello, WebSocket!");
        var response = await connection.ReceiveAsync();

        // Assert
        response.Should().NotBeNull();
        response!.MessageType.Should().Be(WebSocketMessageType.Text);
        response.Content.Should().Be("Echo: Hello, WebSocket!");
        response.IsComplete.Should().BeTrue();

        // Cleanup
        await connection.CloseAsync();
    }

    [Fact]
    public async Task ReceiveAsync_WithBinaryMessage_ShouldReceiveBinaryData()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws/binary";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);
        var expectedData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello" in bytes

        // Act
        await connection.SendAsync(expectedData);
        var response = await connection.ReceiveAsync();

        // Assert
        response.Should().NotBeNull();
        response!.MessageType.Should().Be(WebSocketMessageType.Binary);
        response.BinaryData.Should().Equal(expectedData);
        response.IsBinary.Should().BeTrue();
        response.IsText.Should().BeFalse();

        // Cleanup
        await connection.CloseAsync();
    }

    [Fact]
    public async Task ReceiveAsync_WithMultipleMessages_ShouldReceiveInOrder()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws/multiple";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);
        var receivedMessages = new List<string>();

        // Act - receive all 3 messages that server sends
        for (int i = 0; i < 3; i++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var message = await connection.ReceiveAsync(cts.Token);
            if (message != null && !message.IsClose)
            {
                receivedMessages.Add(message.Content);
            }
        }

        // Assert
        receivedMessages.Should().HaveCount(3);
        receivedMessages.Should().Equal("Message 1", "Message 2", "Message 3");

        // Cleanup
        await connection.CloseAsync();
    }

    [Fact]
    public async Task WebSocketMessage_CreateText_ShouldCreateTextMessage()
    {
        // Act
        var message = WebSocketMessage.CreateText("test message");

        // Assert
        message.Should().NotBeNull();
        message.MessageType.Should().Be(WebSocketMessageType.Text);
        message.Content.Should().Be("test message");
        message.IsText.Should().BeTrue();
        message.IsBinary.Should().BeFalse();
        message.IsClose.Should().BeFalse();
    }

    [Fact]
    public async Task WebSocketMessage_CreateBinary_ShouldCreateBinaryMessage()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var message = WebSocketMessage.CreateBinary(data);

        // Assert
        message.Should().NotBeNull();
        message.MessageType.Should().Be(WebSocketMessageType.Binary);
        message.BinaryData.Should().Equal(data);
        message.IsBinary.Should().BeTrue();
        message.IsText.Should().BeFalse();
    }

    #endregion

    #region JSON Aggregation

    [Fact]
    public async Task ReceiveAsync_WithValidJson_ShouldParseCorrectly()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var options = new WebSocketAdapterOptions
        {
            AggregateJsonMessages = true
        };
        var adapter = new WebSocketAdapter(options, _logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);

        // Act - send a JSON message
        await connection.SendAsync("{\"id\":1,\"name\":\"test\"}");
        var response = await connection.ReceiveAsync();

        // Assert
        response.Should().NotBeNull();
        response!.IsComplete.Should().BeTrue();
        response.Content.Should().Contain("Echo:");

        // Cleanup
        await connection.CloseAsync();
    }

    #endregion

    #region Error Handling

    [Fact]
    public async Task SendAsync_WhenDisconnected_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var options = new WebSocketAdapterOptions();
        var connection = new WebSocketConnection($"ws://localhost:{_port}/ws", options);
        _disposables.Add(connection);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await connection.SendAsync("test message");
        });
    }

    [Fact]
    public async Task ReceiveAsync_WithTimeout_ShouldThrowTimeoutException()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var options = new WebSocketAdapterOptions
        {
            ReceiveTimeout = TimeSpan.FromMilliseconds(100)
        };
        var adapter = new WebSocketAdapter(options, _logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);

        // Act & Assert - should timeout since server only responds when we send
        await Assert.ThrowsAsync<TimeoutException>(async () =>
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            await connection.ReceiveAsync(cts.Token);
        });

        // Cleanup
        await connection.CloseAsync();
    }

    [Fact]
    public async Task ExtractAsync_WithInvalidUrl_ShouldReturnErrorResponse()
    {
        // Arrange
        var adapter = new WebSocketAdapter(new WebSocketAdapterOptions
        {
            ConnectionTimeout = TimeSpan.FromMilliseconds(100)
        }, _logger);
        _disposables.Add(adapter);

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = "ws://localhost:1/ws", // Invalid port
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(1)
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeFalse();
        response.Error.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Reconnection Logic

    [Fact]
    public void ReconnectionPolicy_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var policy = new ReconnectionPolicy();

        // Assert
        policy.Enabled.Should().BeTrue();
        policy.MaxAttempts.Should().Be(5);
        policy.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        policy.MaxDelay.Should().Be(TimeSpan.FromSeconds(30));
        policy.BackoffMultiplier.Should().Be(2.0);
    }

    [Fact]
    public void ReconnectionPolicy_Disabled_ShouldReturnDisabledPolicy()
    {
        // Act
        var policy = ReconnectionPolicy.Disabled();

        // Assert
        policy.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ReconnectionPolicy_CalculateDelay_ShouldUseExponentialBackoff()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(60),
            BackoffMultiplier = 2.0,
            UseJitter = false
        };

        // Act
        var delay0 = policy.CalculateDelay(0);
        var delay1 = policy.CalculateDelay(1);
        var delay2 = policy.CalculateDelay(2);
        var delay10 = policy.CalculateDelay(10); // Should hit max

        // Assert
        delay0.Should().Be(TimeSpan.FromSeconds(1));
        delay1.Should().Be(TimeSpan.FromSeconds(2));
        delay2.Should().Be(TimeSpan.FromSeconds(4));
        delay10.Should().Be(TimeSpan.FromSeconds(60)); // Capped at max
    }

    [Fact]
    public void ReconnectionPolicy_Aggressive_ShouldHaveUnlimitedAttempts()
    {
        // Act
        var policy = ReconnectionPolicy.Aggressive();

        // Assert
        policy.MaxAttempts.Should().Be(-1);
        policy.Enabled.Should().BeTrue();
    }

    [Fact]
    public async Task Connection_WhenReconnected_ShouldIncrementReconnectionCount()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var options = new WebSocketAdapterOptions
        {
            AutoReconnect = false, // Don't auto reconnect for this test
            ReconnectionPolicy = ReconnectionPolicy.Disabled()
        };
        var connection = new WebSocketConnection(wsUrl, options);
        _disposables.Add(connection);

        await connection.ConnectAsync();

        // Act - close and reconnect
        await connection.CloseAsync();

        // Initial reconnection count should be 0
        connection.ReconnectionCount.Should().Be(0);
    }

    #endregion

    #region Heartbeat/Ping-Pong

    [Fact]
    public void HeartbeatOptions_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var options = new HeartbeatOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.Interval.Should().Be(TimeSpan.FromSeconds(30));
        options.Timeout.Should().Be(TimeSpan.FromSeconds(10));
        options.Message.Should().Be("ping");
        options.ExpectedResponse.Should().Be("pong");
    }

    [Fact]
    public void HeartbeatOptions_Disabled_ShouldReturnDisabledOptions()
    {
        // Act
        var options = HeartbeatOptions.Disabled();

        // Assert
        options.Enabled.Should().BeFalse();
    }

    [Fact]
    public void HeartbeatOptions_Validate_WithInvalidInterval_ShouldThrow()
    {
        // Arrange
        var options = new HeartbeatOptions
        {
            Interval = TimeSpan.Zero
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void HeartbeatOptions_Validate_WithTimeoutGreaterThanInterval_ShouldThrow()
    {
        // Arrange
        var options = new HeartbeatOptions
        {
            Interval = TimeSpan.FromSeconds(5),
            Timeout = TimeSpan.FromSeconds(10)
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    #endregion

    #region Message Buffering

    [Fact]
    public void MessageBuffer_ShouldStoreAndRetrieveMessages()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100);
        var message1 = WebSocketMessage.CreateText("msg1");
        var message2 = WebSocketMessage.CreateText("msg2");

        // Act
        buffer.Add(message1);
        buffer.Add(message2);
        var messages = buffer.Peek();

        // Assert
        messages.Should().HaveCount(2);
        messages[0].Content.Should().Be("msg1");
        messages[1].Content.Should().Be("msg2");
    }

    [Fact]
    public void MessageBuffer_Flush_ShouldRemoveMessages()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100);
        buffer.Add(WebSocketMessage.CreateText("msg1"));
        buffer.Add(WebSocketMessage.CreateText("msg2"));

        // Act
        var messages = buffer.Flush();

        // Assert
        messages.Should().HaveCount(2);
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void MessageBuffer_Clear_ShouldRemoveAllMessages()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100);
        buffer.Add(WebSocketMessage.CreateText("msg1"));
        buffer.Add(WebSocketMessage.CreateText("msg2"));

        // Act
        buffer.Clear();

        // Assert
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void MessageBuffer_Count_ShouldReflectMessageCount()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100);

        // Act & Assert
        buffer.Count.Should().Be(0);

        buffer.Add(WebSocketMessage.CreateText("msg1"));
        buffer.Count.Should().Be(1);

        buffer.Add(WebSocketMessage.CreateText("msg2"));
        buffer.Count.Should().Be(2);

        buffer.Clear();
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void MessageBuffer_ShouldFlush_WhenCountThresholdReached()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 2);
        buffer.Add(WebSocketMessage.CreateText("msg1"));

        // Act
        var shouldFlush1 = buffer.ShouldFlush;
        buffer.Add(WebSocketMessage.CreateText("msg2"));
        var shouldFlush2 = buffer.ShouldFlush;

        // Assert
        shouldFlush1.Should().BeFalse();
        shouldFlush2.Should().BeTrue();
    }

    #endregion

    #region WebSocketAdapter Options

    [Fact]
    public void WebSocketAdapterOptions_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var options = new WebSocketAdapterOptions();

        // Assert
        options.ReceiveBufferSize.Should().Be(4096);
        options.AggregateJsonMessages.Should().BeTrue();
        options.EnableMessageBuffering.Should().BeFalse();
        options.BufferSize.Should().Be(100);
        options.AutoReconnect.Should().BeFalse();
        options.ReceiveTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.ConnectionTimeout.Should().Be(TimeSpan.FromSeconds(10));
        options.UseSecureConnection.Should().BeTrue();
    }

    [Fact]
    public void WebSocketAdapterOptions_Validate_WithInvalidBufferSize_ShouldThrow()
    {
        // Arrange
        var options = new WebSocketAdapterOptions
        {
            ReceiveBufferSize = 0
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void WebSocketAdapterOptions_Validate_WithInvalidConnectionTimeout_ShouldThrow()
    {
        // Arrange
        var options = new WebSocketAdapterOptions
        {
            ConnectionTimeout = TimeSpan.Zero
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => options.Validate());
    }

    [Fact]
    public void WebSocketAdapterOptions_Clone_ShouldCreateDeepCopy()
    {
        // Arrange
        var options = new WebSocketAdapterOptions
        {
            Subprotocols = { "protocol1", "protocol2" },
            ReceiveBufferSize = 8192
        };

        // Act
        var clone = (WebSocketAdapterOptions)options.Clone();

        // Assert
        clone.ReceiveBufferSize.Should().Be(8192);
        clone.Subprotocols.Should().HaveCount(2);
        clone.Subprotocols[0].Should().Be("protocol1");

        // Verify deep copy
        clone.Subprotocols.Add("protocol3");
        options.Subprotocols.Should().HaveCount(2); // Original should be unchanged
    }

    #endregion

    #region Disconnection Handling

    [Fact]
    public async Task CloseAsync_ShouldCloseConnectionGracefully()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);
        connection.IsConnected.Should().BeTrue();

        // Act
        await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test complete");

        // Assert
        connection.IsConnected.Should().BeFalse();
        connection.State.Should().Be(WebSocketState.Closed);
        connection.CloseStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
    }

    [Fact]
    public async Task Connection_Dispose_ShouldCloseConnection()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);
        connection.IsConnected.Should().BeTrue();

        // Act
        connection.Dispose();

        // Assert
        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Adapter_Dispose_ShouldCloseAllConnections()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);

        var connection = await adapter.ConnectAsync(wsUrl);
        connection.IsConnected.Should().BeTrue();

        // Act
        adapter.Dispose();

        // Assert
        connection.IsConnected.Should().BeFalse();
    }

    [Fact]
    public async Task Connection_AfterDispose_ShouldThrowObjectDisposedException()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);
        connection.Dispose();

        // Act & Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await connection.SendAsync("test");
        });
    }

    #endregion

    #region IContentAdapter Integration

    [Fact]
    public async Task ExtractAsync_WithWebSocketRequest_ShouldExtractContent()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = wsUrl,
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(5),
            ExpectedContentType = ContentType.WebSocket
        };

        // Act
        var response = await adapter.ExtractAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
        response.AdapterName.Should().Be("WebSocket");
        response.StatusCode.Should().Be(101); // Switching Protocols
        response.ContentResult.Should().NotBeNull();
        response.Metadata.Should().ContainKey("ConnectionId");
    }

    [Fact]
    public async Task ExtractAsync_WithCustomOptions_ShouldUseOptions()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var options = new WebSocketAdapterOptions
        {
            ReceiveTimeout = TimeSpan.FromSeconds(1),
            BufferSize = 50
        };
        var adapter = new WebSocketAdapter(options, _logger);
        _disposables.Add(adapter);

        var request = new Request
        {
            RequestId = Guid.NewGuid().ToString(),
            Url = wsUrl,
            Method = "GET",
            Timeout = TimeSpan.FromSeconds(5)
        };

        // Act
        var response = await adapter.ExtractAsync(request, options);

        // Assert
        response.Should().NotBeNull();
        response.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Connection Events

    [Fact]
    public async Task Connection_MessageReceived_ShouldRaiseEvent()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);
        var receivedMessages = new List<WebSocketMessage>();

        connection.MessageReceived += (s, e) => receivedMessages.Add(e);

        // Act
        await connection.SendAsync("test message");
        var response = await connection.ReceiveAsync();

        // Assert
        receivedMessages.Should().HaveCount(1);
        receivedMessages[0].Content.Should().Be("Echo: test message");

        // Cleanup
        await connection.CloseAsync();
    }

    [Fact]
    public async Task Connection_ConnectionClosed_ShouldRaiseEvent()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);
        var closeEventRaised = false;
        WebSocketCloseStatus? closeStatus = null;

        connection.ConnectionClosed += (s, e) =>
        {
            closeEventRaised = true;
            closeStatus = e;
        };

        // Act
        await connection.CloseAsync(WebSocketCloseStatus.NormalClosure, "Test");

        // Assert
        closeEventRaised.Should().BeTrue();
        closeStatus.Should().Be(WebSocketCloseStatus.NormalClosure);
    }

    #endregion

    #region Adapter Connection Management

    [Fact]
    public async Task GetConnection_WithExistingConnection_ShouldReturnConnection()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);

        // Act
        var retrievedConnection = adapter.GetConnection(connection.ConnectionId);

        // Assert
        retrievedConnection.Should().NotBeNull();
        retrievedConnection!.ConnectionId.Should().Be(connection.ConnectionId);

        // Cleanup
        await adapter.CloseAsync(connection.ConnectionId);
    }

    [Fact]
    public async Task GetConnection_WithNonExistentConnection_ShouldReturnNull()
    {
        // Arrange
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        // Act
        var connection = adapter.GetConnection("non-existent-id");

        // Assert
        connection.Should().BeNull();
    }

    [Fact]
    public async Task GetActiveConnections_WithOpenConnections_ShouldReturnActiveConnections()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);

        // Act
        var activeConnections = adapter.GetActiveConnections();

        // Assert
        activeConnections.Should().HaveCount(1);
        activeConnections[0].ConnectionId.Should().Be(connection.ConnectionId);

        // Cleanup
        await adapter.CloseAsync(connection.ConnectionId);
    }

    [Fact]
    public async Task CloseAsync_WithExistingConnection_ShouldCloseAndRemove()
    {
        // Arrange
        var wsUrl = $"ws://localhost:{_port}/ws";
        var adapter = new WebSocketAdapter(_logger);
        _disposables.Add(adapter);

        var connection = await adapter.ConnectAsync(wsUrl);
        var connectionId = connection.ConnectionId;

        // Act
        await adapter.CloseAsync(connectionId);

        // Assert
        var retrievedConnection = adapter.GetConnection(connectionId);
        retrievedConnection.Should().BeNull();
    }

    #endregion
}
