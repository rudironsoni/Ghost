using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class DnsCacheOptionsTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var options = new DnsCacheOptions();

        // Assert
        options.Ttl.Should().Be(TimeSpan.FromMinutes(5));
        options.MaxEntries.Should().Be(1000);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void Ttl_CanBeSet()
    {
        // Arrange
        var options = new DnsCacheOptions();
        var newTtl = TimeSpan.FromMinutes(10);

        // Act
        options.Ttl = newTtl;

        // Assert
        options.Ttl.Should().Be(newTtl);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void MaxEntries_CanBeSet()
    {
        // Arrange
        var options = new DnsCacheOptions();
        const int newMaxEntries = 5000;

        // Act
        options.MaxEntries = newMaxEntries;

        // Assert
        options.MaxEntries.Should().Be(newMaxEntries);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void ObjectInitializer_SetsValues()
    {
        // Act
        var options = new DnsCacheOptions
        {
            Ttl = TimeSpan.FromMinutes(15),
            MaxEntries = 2000
        };

        // Assert
        options.Ttl.Should().Be(TimeSpan.FromMinutes(15));
        options.MaxEntries.Should().Be(2000);
    }
}
