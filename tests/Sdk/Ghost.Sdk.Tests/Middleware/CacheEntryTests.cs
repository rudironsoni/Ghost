using FluentAssertions;
using Ghost.Sdk.Middleware;
using Microsoft.Extensions.Time.Testing;
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
        var fakeTimeProvider = new FakeTimeProvider();
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMinutes(5);

        // Act
        var entry = new CacheEntry(response, expiresAt, fakeTimeProvider);

        // Assert
        entry.Response.Should().Be(response);
        entry.ExpiresAt.Should().Be(expiresAt);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithNullResponse_ThrowsArgumentNullException()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMinutes(5);

        // Act
        var act = () => new CacheEntry(null!, expiresAt, fakeTimeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_BeforeExpirationTime_ReturnsFalse()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var response = CreateMockResponse(200, "OK");
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMinutes(5);
        var entry = new CacheEntry(response, expiresAt, fakeTimeProvider);

        // Act
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_AfterExpirationTime_ReturnsTrue()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var response = CreateMockResponse(200, "OK");
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMilliseconds(100);
        var entry = new CacheEntry(response, expiresAt, fakeTimeProvider);

        // Act
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(150));
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_AtExactExpirationTime_ReturnsTrue()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var response = CreateMockResponse(200, "OK");
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMilliseconds(-1); // Already expired
        var entry = new CacheEntry(response, expiresAt, fakeTimeProvider);

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
        var fakeTimeProvider = new FakeTimeProvider();
        var response = CreateMockResponse(404, "Not Found");
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMinutes(5);
        var entry = new CacheEntry(response, expiresAt, fakeTimeProvider);

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
