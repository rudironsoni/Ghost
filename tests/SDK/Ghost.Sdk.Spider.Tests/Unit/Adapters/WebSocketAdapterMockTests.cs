using FluentAssertions;
using Ghost.Sdk.Spider.Adapters.WebSocket;
using NUnit.Framework;
using System.Net.WebSockets;

namespace Ghost.Sdk.Spider.Tests.Unit.Adapters;

/// <summary>
/// Unit tests for WebSocket adapter components with mocked dependencies.
/// </summary>
[TestFixture]
public class WebSocketAdapterMockTests
{
    [Test]
    public void WebSocketMessage_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var message = new WebSocketMessage
        {
            MessageType = WebSocketMessageType.Text,
            Content = "test data",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        // Assert
        message.MessageType.Should().Be(WebSocketMessageType.Text);
        message.Content.Should().Be("test data");
        message.ReceivedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Test]
    public void WebSocketMessage_CreateText_ShouldWork()
    {
        // Act
        var message = WebSocketMessage.CreateText("Hello World");

        // Assert
        message.IsText.Should().BeTrue();
        message.Content.Should().Be("Hello World");
        message.MessageType.Should().Be(WebSocketMessageType.Text);
    }

    [Test]
    public void WebSocketMessage_CreateBinary_ShouldWork()
    {
        // Arrange
        var data = new byte[] { 1, 2, 3, 4, 5 };

        // Act
        var message = WebSocketMessage.CreateBinary(data);

        // Assert
        message.IsBinary.Should().BeTrue();
        message.BinaryData.Should().Equal(data);
        message.MessageType.Should().Be(WebSocketMessageType.Binary);
    }

    [Test]
    public void WebSocketMessage_IsClose_ShouldWorkCorrectly()
    {
        // Arrange
        var message = new WebSocketMessage
        {
            MessageType = WebSocketMessageType.Close
        };

        // Assert
        message.IsClose.Should().BeTrue();
        message.IsText.Should().BeFalse();
        message.IsBinary.Should().BeFalse();
    }

    [Test]
    public void HeartbeatOptions_ShouldHaveDefaultValues()
    {
        // Arrange & Act
        var options = new HeartbeatOptions();

        // Assert
        options.Enabled.Should().BeTrue();
        options.Interval.Should().Be(TimeSpan.FromSeconds(30));
        options.Message.Should().Be("ping");
    }

    [Test]
    public void HeartbeatOptions_ShouldAllowCustomization()
    {
        // Arrange & Act
        var options = new HeartbeatOptions
        {
            Enabled = false,
            Interval = TimeSpan.FromSeconds(60),
            Message = "PING"
        };

        // Assert
        options.Enabled.Should().BeFalse();
        options.Interval.Should().Be(TimeSpan.FromSeconds(60));
        options.Message.Should().Be("PING");
    }

    [Test]
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

    [Test]
    public void ReconnectionPolicy_CalculateDelay_ShouldUseExponentialBackoff()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            InitialDelay = TimeSpan.FromSeconds(1),
            MaxDelay = TimeSpan.FromSeconds(60),
            BackoffMultiplier = 2.0,
            UseJitter = false // Disable jitter for predictable testing
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

    [Test]
    public void ReconnectionPolicy_Disabled_ShouldNotReconnect()
    {
        // Act
        var policy = ReconnectionPolicy.Disabled();

        // Assert
        policy.Enabled.Should().BeFalse();
    }

    [Test]
    public void ReconnectionPolicy_Aggressive_ShouldHaveUnlimitedAttempts()
    {
        // Act
        var policy = ReconnectionPolicy.Aggressive();

        // Assert
        policy.MaxAttempts.Should().Be(-1);
        policy.Enabled.Should().BeTrue();
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
    public void MessageBuffer_ToJsonArray_ShouldSerializeMessages()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100);
        buffer.Add(WebSocketMessage.CreateText("{\"id\":1}"));
        buffer.Add(WebSocketMessage.CreateText("{\"id\":2}"));

        // Act
        var json = buffer.ToJsonArray();

        // Assert
        json.Should().Contain("\"id\":1");
        json.Should().Contain("\"id\":2");
    }

    [Test]
    public void MessageBuffer_GetStatistics_ShouldReturnCorrectData()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100);
        buffer.Add(WebSocketMessage.CreateText("test"));

        // Act
        var (messageCount, totalSize, age) = buffer.GetStatistics();

        // Assert
        messageCount.Should().Be(1);
        totalSize.Should().BeGreaterThan(0);
        age.Should().BeGreaterOrEqualTo(TimeSpan.Zero);
    }

    [Test]
    public void MessageBuffer_AddNull_ShouldThrowArgumentNullException()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100);

        // Act & Assert
        buffer.Invoking(b => b.Add(null!))
            .Should().Throw<ArgumentNullException>();
    }

    [Test]
    public void ReconnectionPolicy_Validate_WithValidSettings_ShouldNotThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy();

        // Act & Assert
        policy.Invoking(p => p.Validate()).Should().NotThrow();
    }

    [Test]
    public void ReconnectionPolicy_Validate_WithInvalidMaxAttempts_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            MaxAttempts = 0
        };

        // Act & Assert
        policy.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*MaxAttempts*");
    }

    [Test]
    public void ReconnectionPolicy_Validate_WithInvalidBackoffMultiplier_ShouldThrow()
    {
        // Arrange
        var policy = new ReconnectionPolicy
        {
            BackoffMultiplier = 0.5
        };

        // Act & Assert
        policy.Invoking(p => p.Validate())
            .Should().Throw<ArgumentException>()
            .WithMessage("*BackoffMultiplier*");
    }

    [Test]
    public void MessageBuffer_IsEmpty_ShouldReflectState()
    {
        // Arrange
        var buffer = new MessageBuffer(maxMessageCount: 100);

        // Act & Assert
        buffer.IsEmpty.Should().BeTrue();
        
        buffer.Add(WebSocketMessage.CreateText("msg"));
        buffer.IsEmpty.Should().BeFalse();
        
        buffer.Clear();
        buffer.IsEmpty.Should().BeTrue();
    }
}
