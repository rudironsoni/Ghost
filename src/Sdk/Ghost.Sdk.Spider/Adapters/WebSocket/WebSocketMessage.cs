using System.Net.WebSockets;

namespace Ghost.Sdk.Spider.Adapters.WebSocket;

/// <summary>
/// Represents a WebSocket message with its content and metadata.
/// </summary>
/// <remarks>
/// This class encapsulates a single WebSocket message, including its type
/// (text or binary), content, and timing information.
/// </remarks>
public class WebSocketMessage
{
    /// <summary>
    /// Gets or sets the message type.
    /// </summary>
    /// <value>The WebSocket message type (Text, Binary, or Close).</value>
    public WebSocketMessageType MessageType { get; set; }

    /// <summary>
    /// Gets or sets the message content as a string.
    /// </summary>
    /// <value>
    /// The message content. For text messages, this is the UTF-8 decoded text.
    /// For binary messages, this is the base64-encoded binary data.
    /// </value>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the raw binary data for binary messages.
    /// </summary>
    /// <value>The raw binary content, or null for text messages.</value>
    public byte[]? BinaryData { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the message was received.
    /// </summary>
    /// <value>The UTC timestamp of message reception.</value>
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the message size in bytes.
    /// </summary>
    /// <value>The total size of the message content.</value>
    public int Size { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a complete message.
    /// </summary>
    /// <value>
    /// <c>true</c> if this is the final fragment of the message; otherwise, <c>false</c>.
    /// </value>
    public bool IsComplete { get; set; } = true;

    /// <summary>
    /// Gets a value indicating whether this is a text message.
    /// </summary>
    /// <value><c>true</c> if the message type is Text; otherwise, <c>false</c>.</value>
    public bool IsText => MessageType == WebSocketMessageType.Text;

    /// <summary>
    /// Gets a value indicating whether this is a binary message.
    /// </summary>
    /// <value><c>true</c> if the message type is Binary; otherwise, <c>false</c>.</value>
    public bool IsBinary => MessageType == WebSocketMessageType.Binary;

    /// <summary>
    /// Gets a value indicating whether this is a close message.
    /// </summary>
    /// <value><c>true</c> if the message type is Close; otherwise, <c>false</c>.</value>
    public bool IsClose => MessageType == WebSocketMessageType.Close;

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketMessage"/> class.
    /// </summary>
    public WebSocketMessage()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketMessage"/> class with the specified content.
    /// </summary>
    /// <param name="content">The message content.</param>
    /// <param name="messageType">The message type.</param>
    public WebSocketMessage(string content, WebSocketMessageType messageType = WebSocketMessageType.Text)
    {
        Content = content;
        MessageType = messageType;
        Size = System.Text.Encoding.UTF8.GetByteCount(content);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="WebSocketMessage"/> class with binary data.
    /// </summary>
    /// <param name="binaryData">The binary message data.</param>
    public WebSocketMessage(byte[] binaryData)
    {
        BinaryData = binaryData;
        Content = Convert.ToBase64String(binaryData);
        MessageType = WebSocketMessageType.Binary;
        Size = binaryData.Length;
    }

    /// <summary>
    /// Creates a text message.
    /// </summary>
    /// <param name="content">The text content.</param>
    /// <returns>A new <see cref="WebSocketMessage"/> instance with text content.</returns>
    public static WebSocketMessage CreateText(string content)
    {
        return new WebSocketMessage(content, WebSocketMessageType.Text);
    }

    /// <summary>
    /// Creates a binary message.
    /// </summary>
    /// <param name="data">The binary data.</param>
    /// <returns>A new <see cref="WebSocketMessage"/> instance with binary content.</returns>
    public static WebSocketMessage CreateBinary(byte[] data)
    {
        return new WebSocketMessage(data);
    }

    /// <summary>
    /// Returns a string representation of the message.
    /// </summary>
    /// <returns>A string describing the message type and size.</returns>
    public override string ToString()
    {
        return $"{MessageType} message ({Size} bytes) at {ReceivedAt:O}";
    }
}
