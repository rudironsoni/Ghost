using System.Net;
using FluentAssertions;
using Ghost.Sdk.Middleware;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class DnsCacheEntryTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithValidParameters_SetsProperties()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var addresses = new[] { IPAddress.Loopback, IPAddress.IPv6Loopback };
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMinutes(5);

        // Act
        var entry = new DnsCacheEntry(addresses, expiresAt, fakeTimeProvider);

        // Assert
        entry.Addresses.Should().BeEquivalentTo(addresses);
        entry.ExpiresAt.Should().Be(expiresAt);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_WithNullAddresses_ThrowsArgumentNullException()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMinutes(5);

        // Act
        var act = () => new DnsCacheEntry(null!, expiresAt, fakeTimeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_WithFutureExpiration_ReturnsFalse()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var addresses = new[] { IPAddress.Loopback };
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMinutes(5);
        var entry = new DnsCacheEntry(addresses, expiresAt, fakeTimeProvider);

        // Act
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeFalse();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_WithPastExpiration_ReturnsTrue()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var addresses = new[] { IPAddress.Loopback };
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMilliseconds(50);
        var entry = new DnsCacheEntry(addresses, expiresAt, fakeTimeProvider);

        // Act
        fakeTimeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var isExpired = entry.IsExpired;

        // Assert
        isExpired.Should().BeTrue();
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void IsExpired_AtExpirationTime_ReturnsTrue()
    {
        // Arrange
        var fakeTimeProvider = new FakeTimeProvider();
        var addresses = new[] { IPAddress.Loopback };
        var expiresAt = fakeTimeProvider.GetUtcNow();
        var entry = new DnsCacheEntry(addresses, expiresAt, fakeTimeProvider);

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
        var fakeTimeProvider = new FakeTimeProvider();
        var addresses = new[] { IPAddress.Loopback, IPAddress.IPv6Loopback, IPAddress.Parse("192.168.1.1") };
        var expiresAt = fakeTimeProvider.GetUtcNow().AddMinutes(5);
        var entry = new DnsCacheEntry(addresses, expiresAt, fakeTimeProvider);

        // Act
        var result = entry.Addresses;

        // Assert
        result.Should().BeEquivalentTo(addresses);
        result.Should().HaveCount(3);
    }
}
