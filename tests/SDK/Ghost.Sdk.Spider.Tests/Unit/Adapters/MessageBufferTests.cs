using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.WebSocket;
using NUnit.Framework;
using System.Net.WebSockets;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Comprehensive tests for MessageBuffer class.
/// </summary>
[TestFixture]
public class MessageBufferTests
{
    [Test]
    public void Constructor_WithValidMaxMessageCount_ShouldInitialize()
    {
        // Act
        var buffer = new MessageBuffer(maxMessageCount: 10);

        // Assert
        buffer.Should().NotBeNull();
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Constructor_WithValidMaxWaitTime_ShouldInitialize()
    {
        // Act
        var buffer = new MessageBuffer(maxMessageCount: 0, maxWaitTime: TimeSpan.FromSeconds(5));

        // Assert
        buffer.Should().NotBeNull();
        buffer.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Constructor_WithBothZero_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MessageBuffer(maxMessageCount: 0, maxWaitTime: TimeSpan.Zero));
    }

    [Test]
    public void Constructor_WithNegativeMaxMessageCount_ShouldThrowArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new MessageBuffer(maxMessageCount: -1));
    }

    [Test]
    public void Add_WithValidMessage_ShouldIncreaseCount()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        var message = WebSocketMessage.CreateText("test");

        // Act
        buffer.Add(message);

        // Assert
        buffer.Count.Should().Be(1);
        buffer.IsEmpty.Should().BeFalse();
    }

    [Test]
    public void Add_WithNullMessage_ShouldThrowArgumentNullException()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => buffer.Add(null!));
    }

    [Test]
    public void Add_MultipleMessages_ShouldIncreaseCount()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        var message1 = WebSocketMessage.CreateText("test1");
        var message2 = WebSocketMessage.CreateText("test2");
        var message3 = WebSocketMessage.CreateText("test3");

        // Act
        buffer.Add(message1);
        buffer.Add(message2);
        buffer.Add(message3);

        // Assert
        buffer.Count.Should().Be(3);
    }

    [Test]
    public void Peek_WithMessages_ShouldReturnMessagesWithoutRemoving()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        var message1 = WebSocketMessage.CreateText("test1");
        var message2 = WebSocketMessage.CreateText("test2");
        buffer.Add(message1);
        buffer.Add(message2);

        // Act
        var messages = buffer.Peek();

        // Assert
        messages.Should().HaveCount(2);
        buffer.Count.Should().Be(2); // Should not remove messages
        messages[0].Content.Should().Be("test1");
        messages[1].Content.Should().Be("test2");
    }

    [Test]
    public void Peek_WithEmptyBuffer_ShouldReturnEmptyArray()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);

        // Act
        var messages = buffer.Peek();

        // Assert
        messages.Should().BeEmpty();
    }

    [Test]
    public void Flush_WithMessages_ShouldReturnAndRemoveAll()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        var message1 = WebSocketMessage.CreateText("test1");
        var message2 = WebSocketMessage.CreateText("test2");
        buffer.Add(message1);
        buffer.Add(message2);

        // Act
        var messages = buffer.Flush();

        // Assert
        messages.Should().HaveCount(2);
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Flush_WithEmptyBuffer_ShouldReturnEmptyArray()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);

        // Act
        var messages = buffer.Flush();

        // Assert
        messages.Should().BeEmpty();
        buffer.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void ShouldFlush_WithMaxMessageCountReached_ShouldReturnTrue()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 2);
        buffer.Add(WebSocketMessage.CreateText("test1"));
        buffer.Add(WebSocketMessage.CreateText("test2"));

        // Act
        var shouldFlush = buffer.ShouldFlush;

        // Assert
        shouldFlush.Should().BeTrue();
    }

    [Test]
    public void ShouldFlush_WithLessThanMaxMessages_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 5);
        buffer.Add(WebSocketMessage.CreateText("test1"));

        // Act
        var shouldFlush = buffer.ShouldFlush;

        // Assert
        shouldFlush.Should().BeFalse();
    }

    [Test]
    public void ShouldFlush_WithEmptyBuffer_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);

        // Act
        var shouldFlush = buffer.ShouldFlush;

        // Assert
        shouldFlush.Should().BeFalse();
    }

    [Test]
    public void ShouldFlush_WithMaxWaitTimeExceeded_ShouldReturnTrue()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100, maxWaitTime: TimeSpan.FromMilliseconds(10));
        buffer.Add(WebSocketMessage.CreateText("test"));

        // Act
        Thread.Sleep(15); // Wait for timeout to be exceeded
        var shouldFlush = buffer.ShouldFlush;

        // Assert
        shouldFlush.Should().BeTrue();
    }

    [Test]
    public void ToJsonArray_WithTextMessages_WithoutMetadata_ShouldReturnJsonArray()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        buffer.Add(WebSocketMessage.CreateText("{\"id\":1}"));
        buffer.Add(WebSocketMessage.CreateText("{\"id\":2}"));

        // Act
        var json = buffer.ToJsonArray(includeMetadata: false);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("\"id\"");
        json.Should().Contain("1");
        json.Should().Contain("2");
    }

    [Test]
    public void ToJsonArray_WithTextMessages_WithMetadata_ShouldIncludeMetadata()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        buffer.Add(WebSocketMessage.CreateText("test"));

        // Act
        var json = buffer.ToJsonArray(includeMetadata: true);

        // Assert
        json.Should().Contain("type");
        json.Should().Contain("content");
        json.Should().Contain("receivedAt");
        json.Should().Contain("size");
        json.Should().Contain("isComplete");
    }

    [Test]
    public void ToJsonArray_WithEmptyBuffer_ShouldReturnEmptyArray()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);

        // Act
        var json = buffer.ToJsonArray();

        // Assert
        json.Should().Be("[]");
    }

    [Test]
    public void ToJsonArray_WithNonJsonTextMessage_ShouldIncludeAsString()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        buffer.Add(WebSocketMessage.CreateText("plain text message"));

        // Act
        var json = buffer.ToJsonArray(includeMetadata: false);

        // Assert
        json.Should().Contain("plain text message");
    }

    [Test]
    public void ToJsonArray_WithBinaryMessage_ShouldIncludeBase64()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        var binaryData = new byte[] { 0x48, 0x65, 0x6C, 0x6C, 0x6F }; // "Hello"
        buffer.Add(WebSocketMessage.CreateBinary(binaryData));

        // Act
        var json = buffer.ToJsonArray(includeMetadata: false);

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain(Convert.ToBase64String(binaryData));
    }

    [Test]
    public void ToJsonArray_ShouldNotRemoveMessages()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        buffer.Add(WebSocketMessage.CreateText("test"));

        // Act
        buffer.ToJsonArray();

        // Assert
        buffer.Count.Should().Be(1); // Messages should still be there
    }

    [Test]
    public void FlushToJsonArray_WithMessages_ShouldReturnJsonAndClearBuffer()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        buffer.Add(WebSocketMessage.CreateText("{\"test\":true}"));

        // Act
        var json = buffer.FlushToJsonArray();

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("test");
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void FlushToJsonArray_WithMetadata_ShouldIncludeMetadataAndClearBuffer()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        buffer.Add(WebSocketMessage.CreateText("test"));

        // Act
        var json = buffer.FlushToJsonArray(includeMetadata: true);

        // Assert
        json.Should().Contain("type");
        json.Should().Contain("content");
        buffer.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Clear_WithMessages_ShouldRemoveAllMessages()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        buffer.Add(WebSocketMessage.CreateText("test1"));
        buffer.Add(WebSocketMessage.CreateText("test2"));
        buffer.Add(WebSocketMessage.CreateText("test3"));

        // Act
        buffer.Clear();

        // Assert
        buffer.Count.Should().Be(0);
        buffer.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void Clear_WithEmptyBuffer_ShouldNotThrow()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);

        // Act
        buffer.Clear();

        // Assert
        buffer.IsEmpty.Should().BeTrue();
    }

    [Test]
    public void GetStatistics_WithMessages_ShouldReturnCorrectStats()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);
        buffer.Add(WebSocketMessage.CreateText("test1")); // 5 bytes
        buffer.Add(WebSocketMessage.CreateText("test2")); // 5 bytes
        buffer.Add(WebSocketMessage.CreateText("test3")); // 5 bytes

        // Act
        var (messageCount, totalSize, age) = buffer.GetStatistics();

        // Assert
        messageCount.Should().Be(3);
        totalSize.Should().Be(15);
        age.Should().BeGreaterThanOrEqualTo(TimeSpan.Zero);
    }

    [Test]
    public void GetStatistics_WithEmptyBuffer_ShouldReturnZeroStats()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 10);

        // Act
        var (messageCount, totalSize, age) = buffer.GetStatistics();

        // Assert
        messageCount.Should().Be(0);
        totalSize.Should().Be(0);
        age.Should().Be(TimeSpan.Zero);
    }

    [Test]
    public void WebSocketMessage_CreateText_ShouldSetPropertiesCorrectly()
    {
        // Act
        var message = WebSocketMessage.CreateText("Hello");

        // Assert
        message.MessageType.Should().Be(WebSocketMessageType.Text);
        message.Content.Should().Be("Hello");
        message.IsText.Should().BeTrue();
        message.IsBinary.Should().BeFalse();
        message.IsClose.Should().BeFalse();
        message.Size.Should().Be(5);
    }

    [Test]
    public void WebSocketMessage_CreateBinary_ShouldSetPropertiesCorrectly()
    {
        // Arrange
        var data = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var message = WebSocketMessage.CreateBinary(data);

        // Assert
        message.MessageType.Should().Be(WebSocketMessageType.Binary);
        message.IsBinary.Should().BeTrue();
        message.IsText.Should().BeFalse();
        message.BinaryData.Should().Equal(data);
        message.Size.Should().Be(3);
    }

    [Test]
    public void WebSocketMessage_Constructor_WithString_ShouldInitialize()
    {
        // Act
        var message = new WebSocketMessage("test content");

        // Assert
        message.Content.Should().Be("test content");
        message.MessageType.Should().Be(WebSocketMessageType.Text);
    }

    [Test]
    public void WebSocketMessage_Constructor_WithBytes_ShouldInitialize()
    {
        // Arrange
        var data = new byte[] { 0x48, 0x65 };

        // Act
        var message = new WebSocketMessage(data);

        // Assert
        message.BinaryData.Should().Equal(data);
        message.MessageType.Should().Be(WebSocketMessageType.Binary);
        message.Content.Should().Be(Convert.ToBase64String(data));
    }

    [Test]
    public void WebSocketMessage_ToString_ShouldReturnFormattedString()
    {
        // Arrange
        var message = WebSocketMessage.CreateText("test");

        // Act
        var str = message.ToString();

        // Assert
        str.Should().Contain("Text message");
        str.Should().Contain("bytes");
    }

    [Test]
    public void MessageBuffer_ConcurrentAdds_ShouldHandleThreadSafely()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 1000);
        var tasks = new List<Task>();

        // Act
        for (int i = 0; i < 10; i++)
        {
            int taskId = i;
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 10; j++)
                {
                    buffer.Add(WebSocketMessage.CreateText($"Task{taskId}-Message{j}"));
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        buffer.Count.Should().Be(100);
    }

    [Test]
    public void ShouldFlush_WithOnlyMaxMessageCount_AndTimeZero_ShouldWork()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 3, maxWaitTime: TimeSpan.Zero);
        buffer.Add(WebSocketMessage.CreateText("test1"));
        buffer.Add(WebSocketMessage.CreateText("test2"));
        buffer.Add(WebSocketMessage.CreateText("test3"));

        // Act
        var shouldFlush = buffer.ShouldFlush;

        // Assert
        shouldFlush.Should().BeTrue();
    }

    [Test]
    public void Add_FirstMessage_ShouldResetTimestamp()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100, maxWaitTime: TimeSpan.FromMilliseconds(50));

        // Act
        buffer.Add(WebSocketMessage.CreateText("test1"));
        Thread.Sleep(20);
        buffer.Flush(); // Clear buffer

        buffer.Add(WebSocketMessage.CreateText("test2")); // Should reset timestamp
        var shouldFlush = buffer.ShouldFlush;

        // Assert
        shouldFlush.Should().BeFalse(); // Time should be reset
    }

    [Test]
    public void WebSocketMessage_DefaultConstructor_ShouldInitializeDefaults()
    {
        // Act
        var message = new WebSocketMessage();

        // Assert
        message.Content.Should().Be(string.Empty);
        message.Size.Should().Be(0);
        message.IsComplete.Should().BeTrue();
        message.ReceivedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void WebSocketMessage_IsClose_WithCloseMessageType_ShouldReturnTrue()
    {
        // Arrange
        var message = new WebSocketMessage
        {
            MessageType = WebSocketMessageType.Close
        };

        // Act & Assert
        message.IsClose.Should().BeTrue();
        message.IsText.Should().BeFalse();
        message.IsBinary.Should().BeFalse();
    }

    [Test]
    public void MessageBuffer_FlushResetsTimestamp()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100, maxWaitTime: TimeSpan.FromSeconds(1));
        buffer.Add(WebSocketMessage.CreateText("test"));

        // Act
        buffer.Flush();
        var (_, _, age) = buffer.GetStatistics();

        // Assert
        age.Should().Be(TimeSpan.Zero);
    }
}
