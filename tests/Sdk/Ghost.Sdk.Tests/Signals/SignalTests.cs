using FluentAssertions;
using Ghost.Sdk.Signals;

namespace Ghost.Sdk.Tests.Signals;

public sealed class SignalTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void SpiderStartedSignal_WithValidData_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var signal = new SpiderStartedSignal(spiderId, timestamp);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void SpiderStartedSignal_WithNullSpiderId_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new SpiderStartedSignal(null!, DateTimeOffset.UtcNow);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void SpiderIdleSignal_WithValidData_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var signal = new SpiderIdleSignal(spiderId, timestamp);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void SpiderClosedSignal_WithReason_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var reason = "Completed successfully";

        // Act
        var signal = new SpiderClosedSignal(spiderId, timestamp, reason);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.Reason.Should().Be(reason);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void SpiderClosedSignal_WithoutReason_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;

        // Act
        var signal = new SpiderClosedSignal(spiderId, timestamp);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.Reason.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void RequestScheduledSignal_WithValidData_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var url = "https://example.com";
        var method = "POST";

        // Act
        var signal = new RequestScheduledSignal(spiderId, timestamp, url, method);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.Url.Should().Be(url);
        signal.Method.Should().Be(method);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void RequestScheduledSignal_WithDefaultMethod_UsesGet()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var url = "https://example.com";

        // Act
        var signal = new RequestScheduledSignal(spiderId, timestamp, url);

        // Assert
        signal.Method.Should().Be("GET");
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void RequestDroppedSignal_WithValidData_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var url = "https://example.com";
        var reason = "Rate limited";

        // Act
        var signal = new RequestDroppedSignal(spiderId, timestamp, url, reason);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.Url.Should().Be(url);
        signal.Reason.Should().Be(reason);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void ResponseReceivedSignal_WithValidData_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var url = "https://example.com";
        var statusCode = 200;
        var durationMs = 150L;

        // Act
        var signal = new ResponseReceivedSignal(spiderId, timestamp, url, statusCode, durationMs);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.Url.Should().Be(url);
        signal.StatusCode.Should().Be(statusCode);
        signal.DurationMs.Should().Be(durationMs);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void ItemScrapedSignal_WithValidData_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var itemType = "JobListing";
        var url = "https://example.com/job/123";

        // Act
        var signal = new ItemScrapedSignal(spiderId, timestamp, itemType, url);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.ItemType.Should().Be(itemType);
        signal.Url.Should().Be(url);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void ItemDroppedSignal_WithValidData_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var itemType = "JobListing";
        var url = "https://example.com/job/123";
        var reason = "Validation failed";

        // Act
        var signal = new ItemDroppedSignal(spiderId, timestamp, itemType, url, reason);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.ItemType.Should().Be(itemType);
        signal.Url.Should().Be(url);
        signal.Reason.Should().Be(reason);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void ErrorSignal_WithStackTrace_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var message = "Connection failed";
        var exceptionType = "System.Net.Http.HttpRequestException";
        var stackTrace = "at System.Net.Http...";

        // Act
        var signal = new ErrorSignal(spiderId, timestamp, message, exceptionType, stackTrace);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.Message.Should().Be(message);
        signal.ExceptionType.Should().Be(exceptionType);
        signal.StackTrace.Should().Be(stackTrace);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void ErrorSignal_WithoutStackTrace_CreatesInstance()
    {
        // Arrange
        var spiderId = "spider-1";
        var timestamp = DateTimeOffset.UtcNow;
        var message = "Connection failed";
        var exceptionType = "System.Net.Http.HttpRequestException";

        // Act
        var signal = new ErrorSignal(spiderId, timestamp, message, exceptionType);

        // Assert
        signal.SpiderId.Should().Be(spiderId);
        signal.Timestamp.Should().Be(timestamp);
        signal.Message.Should().Be(message);
        signal.ExceptionType.Should().Be(exceptionType);
        signal.StackTrace.Should().BeNull();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void ErrorSignal_WithNullMessage_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ErrorSignal("spider-1", DateTimeOffset.UtcNow, null!, "Exception");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void RequestScheduledSignal_WithNullUrl_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new RequestScheduledSignal("spider-1", DateTimeOffset.UtcNow, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void ItemScrapedSignal_WithNullItemType_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new ItemScrapedSignal("spider-1", DateTimeOffset.UtcNow, null!, "https://example.com");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
