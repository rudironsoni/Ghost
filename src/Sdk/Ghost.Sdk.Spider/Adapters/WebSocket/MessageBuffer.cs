using System.Collections.Concurrent;
using System.Text.Json;

namespace Ghost.Sdk.Spider.Adapters.WebSocket;

/// <summary>
/// Buffers and aggregates WebSocket messages for batch processing.
/// </summary>
/// <remarks>
/// This class collects incoming WebSocket messages and aggregates them based on
/// count or time thresholds. Messages can be aggregated into JSON arrays for
/// structured processing.
/// </remarks>
public class MessageBuffer
{
    private readonly ConcurrentQueue<WebSocketMessage> _messages = new();
    private readonly int _maxMessageCount;
    private readonly TimeSpan _maxWaitTime;
    private DateTimeOffset _firstMessageTime;
    private readonly object _lock = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBuffer"/> class.
    /// </summary>
    /// <param name="maxMessageCount">
    /// The maximum number of messages to buffer before flushing.
    /// Set to 0 for no count limit.
    /// </param>
    /// <param name="maxWaitTime">
    /// The maximum time to wait before flushing messages.
    /// Set to <see cref="TimeSpan.Zero"/> for no time limit.
    /// </param>
    public MessageBuffer(int maxMessageCount = 100, TimeSpan maxWaitTime = default)
        : this(maxMessageCount, maxWaitTime, TimeProvider.System)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBuffer"/> class.
    /// </summary>
    /// <param name="maxMessageCount">
    /// The maximum number of messages to buffer before flushing.
    /// Set to 0 for no count limit.
    /// </param>
    /// <param name="maxWaitTime">
    /// The maximum time to wait before flushing messages.
    /// Set to <see cref="TimeSpan.Zero"/> for no time limit.
    /// </param>
    /// <param name="timeProvider">The time provider for time-based operations.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when both maxMessageCount and maxWaitTime are zero or negative.
    /// </exception>
    public MessageBuffer(int maxMessageCount, TimeSpan maxWaitTime, TimeProvider timeProvider)
    {
        if (maxMessageCount <= 0 && maxWaitTime <= TimeSpan.Zero)
        {
            throw new ArgumentException("Either maxMessageCount or maxWaitTime must be greater than zero.");
        }

        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _maxMessageCount = maxMessageCount;
        _maxWaitTime = maxWaitTime == default ? TimeSpan.Zero : maxWaitTime;
        _firstMessageTime = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// Gets the current number of messages in the buffer.
    /// </summary>
    /// <value>The count of buffered messages.</value>
    public int Count => _messages.Count;

    /// <summary>
    /// Gets a value indicating whether the buffer is empty.
    /// </summary>
    /// <value><c>true</c> if the buffer contains no messages; otherwise, <c>false</c>.</value>
    public bool IsEmpty => _messages.IsEmpty;

    /// <summary>
    /// Gets a value indicating whether the buffer should be flushed.
    /// </summary>
    /// <value>
    /// <c>true</c> if the buffer has reached the message count threshold or time threshold;
    /// otherwise, <c>false</c>.
    /// </value>
    public bool ShouldFlush
    {
        get
        {
            if (IsEmpty)
            {
                return false;
            }

            if (_maxMessageCount > 0 && Count >= _maxMessageCount)
            {
                return true;
            }

            if (_maxWaitTime > TimeSpan.Zero)
            {
                TimeSpan elapsed = _timeProvider.GetUtcNow() - _firstMessageTime;
                return elapsed >= _maxWaitTime;
            }

            return false;
        }
    }

    /// <summary>
    /// Adds a message to the buffer.
    /// </summary>
    /// <param name="message">The WebSocket message to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="message"/> is null.</exception>
    public void Add(WebSocketMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        lock (_lock)
        {
            if (_messages.IsEmpty)
            {
                _firstMessageTime = _timeProvider.GetUtcNow();
            }

            _messages.Enqueue(message);
        }
    }

    /// <summary>
    /// Retrieves all messages from the buffer without removing them.
    /// </summary>
    /// <returns>An array of all buffered messages.</returns>
    public WebSocketMessage[] Peek()
    {
        return _messages.ToArray();
    }

    /// <summary>
    /// Retrieves and removes all messages from the buffer.
    /// </summary>
    /// <returns>An array of all buffered messages.</returns>
    public WebSocketMessage[] Flush()
    {
        List<WebSocketMessage> messages = [];

        lock (_lock)
        {
            while (_messages.TryDequeue(out WebSocketMessage? message))
            {
                messages.Add(message);
            }

            _firstMessageTime = _timeProvider.GetUtcNow();
        }

        return messages.ToArray();
    }

    /// <summary>
    /// Aggregates buffered messages into a JSON array string.
    /// </summary>
    /// <param name="includeMetadata">
    /// If <c>true</c>, includes metadata (timestamp, size) for each message.
    /// If <c>false</c>, only includes message content.
    /// </param>
    /// <returns>A JSON array string containing all buffered messages.</returns>
    /// <remarks>
    /// For text messages, the content is parsed as JSON if possible, otherwise
    /// included as a string. Binary messages are included as base64-encoded strings.
    /// </remarks>
    public string ToJsonArray(bool includeMetadata = false)
    {
        WebSocketMessage[] messages = Peek();

        if (messages.Length == 0)
        {
            return "[]";
        }

        if (includeMetadata)
        {
            return JsonSerializer.Serialize(messages.Select(m => new
            {
                type = m.MessageType.ToString(),
                content = m.Content,
                receivedAt = m.ReceivedAt,
                size = m.Size,
                isComplete = m.IsComplete
            }));
        }

        List<object> contentArray = [];

        foreach (WebSocketMessage message in messages)
        {
            if (message.IsText)
            {
                // Try to parse as JSON
                try
                {
                    JsonElement jsonElement = JsonSerializer.Deserialize<JsonElement>(message.Content);
                    contentArray.Add(jsonElement);
                }
                catch
                {
                    // Not valid JSON, add as string
                    contentArray.Add(message.Content);
                }
            }
            else if (message.IsBinary)
            {
                contentArray.Add(message.Content); // Base64-encoded
            }
        }

        return JsonSerializer.Serialize(contentArray);
    }

    /// <summary>
    /// Aggregates and removes buffered messages into a JSON array string.
    /// </summary>
    /// <param name="includeMetadata">
    /// If <c>true</c>, includes metadata (timestamp, size) for each message.
    /// If <c>false</c>, only includes message content.
    /// </param>
    /// <returns>A JSON array string containing all buffered messages.</returns>
    public string FlushToJsonArray(bool includeMetadata = false)
    {
        string json = ToJsonArray(includeMetadata);
        Flush();
        return json;
    }

    /// <summary>
    /// Clears all messages from the buffer.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            while (_messages.TryDequeue(out _))
            {
                // Drain the queue
            }

            _firstMessageTime = _timeProvider.GetUtcNow();
        }
    }

    /// <summary>
    /// Gets statistics about the buffered messages.
    /// </summary>
    /// <returns>A tuple containing message count, total size, and age of oldest message.</returns>
    public (int MessageCount, long TotalSize, TimeSpan Age) GetStatistics()
    {
        WebSocketMessage[] messages = Peek();
        long totalSize = messages.Sum(m => (long)m.Size);
        TimeSpan age = IsEmpty ? TimeSpan.Zero : _timeProvider.GetUtcNow() - _firstMessageTime;

        return (messages.Length, totalSize, age);
    }
}
