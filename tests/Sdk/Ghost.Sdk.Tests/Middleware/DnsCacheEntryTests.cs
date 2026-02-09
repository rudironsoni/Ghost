using System.Net;
using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class DnsCacheEntryTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // Arrange
        var addresses = new[] { IPAddress.Loopback, IPAddress.IPv6Loopback };
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        var entry = new DnsCacheEntry(addresses, expiresAt);

        // Assert
        entry.Addresses.Should().BeEquivalentTo(addresses);
        entry.ExpiresAt.Should().Be(expiresAt);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithNullAddresses_ThrowsArgumentNullException()
    {
        // Arrange
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);

        // Act
        var act = () => new DnsCacheEntry(null!, expiresAt);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_WithFutureExpiration_ReturnsFalse()
    {
        // Arrange
        var addresses = new[] { IPAddress.Loopback };
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var entry = new DnsCacheEntry(addresses, expiresAt);

        // Act
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public async Task IsExpired_WithPastExpiration_ReturnsTrue()
    {
        // Arrange
        var addresses = new[] { IPAddress.Loopback };
        var expiresAt = DateTimeOffset.UtcNow.AddMilliseconds(50);
        var entry = new DnsCacheEntry(addresses, expiresAt);

        // Act
        await Task.Delay(100); // Wait for expiration
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_AtExpirationTime_ReturnsTrue()
    {
        // Arrange
        var addresses = new[] { IPAddress.Loopback };
        var expiresAt = DateTimeOffset.UtcNow;
        var entry = new DnsCacheEntry(addresses, expiresAt);

        // Act
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Addresses_ReturnsOriginalArray()
    {
        // Arrange
        var addresses = new[] { IPAddress.Loopback, IPAddress.IPv6Loopback, IPAddress.Parse("192.168.1.1") };
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var entry = new DnsCacheEntry(addresses, expiresAt);

        // Act
        var result = entry.Addresses;

        // Assert
        result.Should().BeEquivalentTo(addresses);
        result.Should().HaveCount(3);
    }
}
