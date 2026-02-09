using FluentAssertions;
using Ghost.Sdk.Middleware;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class CacheEntryTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithValidArguments_InitializesProperties()
    {
        // Arrange
        var response = CreateMockResponse(200, "OK");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        var entry = new CacheEntry(response, expiresAt);

        // Assert
        entry.Response.Should().Be(response);
        entry.ExpiresAt.Should().Be(expiresAt);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithNullResponse_ThrowsArgumentNullException()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        var act = () => new CacheEntry(null!, expiresAt);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_BeforeExpirationTime_ReturnsFalse()
    {
        // Arrange
        var response = CreateMockResponse(200, "OK");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var entry = new CacheEntry(response, expiresAt);

        // Act
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task IsExpired_AfterExpirationTime_ReturnsTrue()
    {
        // Arrange
        var response = CreateMockResponse(200, "OK");
        var expiresAt = DateTimeOffset.UtcNow.AddMilliseconds(100);
        var entry = new CacheEntry(response, expiresAt);

        // Act
        await Task.Delay(150); // Wait for expiration
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_AtExactExpirationTime_ReturnsTrue()
    {
        // Arrange
        var response = CreateMockResponse(200, "OK");
        var expiresAt = DateTimeOffset.UtcNow.AddMilliseconds(-1); // Already expired
        var entry = new CacheEntry(response, expiresAt);

        // Act
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Response_ReturnsCorrectResponse()
    {
        // Arrange
        var response = CreateMockResponse(404, "Not Found");
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var entry = new CacheEntry(response, expiresAt);

        // Act & Assert
        entry.Response.Status.Should().Be(404);
        entry.Response.StatusText.Should().Be("Not Found");
    }

    private static IResponse CreateMockResponse(int status, string statusText)
    {
        var mock = new Mock<IResponse>();
        mock.Setup(r => r.Status).Returns(status);
        mock.Setup(r => r.StatusText).Returns(statusText);
        return mock.Object;
    }
}
