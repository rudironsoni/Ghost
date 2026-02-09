using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class HttpCacheOptionsTests
{
    [Trait("Category", "Unit")]
    [Fact]
    public void DefaultTtl_HasExpectedDefaultValue()
    {
        // Arrange & Act
        var options = new HttpCacheOptions();

        // Assert
        options.DefaultTtl.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void MaxCacheSize_HasExpectedDefaultValue()
    {
        // Arrange & Act
        var options = new HttpCacheOptions();

        // Assert
        options.MaxCacheSize.Should().Be(100 * 1024 * 1024); // 100MB
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void CleanupInterval_HasExpectedDefaultValue()
    {
        // Arrange & Act
        var options = new HttpCacheOptions();

        // Assert
        options.CleanupInterval.Should().Be(TimeSpan.FromMinutes(1));
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void DefaultTtl_CanBeSetToCustomValue()
    {
        // Arrange
        var options = new HttpCacheOptions();
        var customTtl = TimeSpan.FromMinutes(10);

        // Act
        options.DefaultTtl = customTtl;

        // Assert
        options.DefaultTtl.Should().Be(customTtl);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void MaxCacheSize_CanBeSetToCustomValue()
    {
        // Arrange
        var options = new HttpCacheOptions();
        var customSize = 200L * 1024 * 1024; // 200MB

        // Act
        options.MaxCacheSize = customSize;

        // Assert
        options.MaxCacheSize.Should().Be(customSize);
    }

    [Trait("Category", "Unit")]
    [Fact]
    public void CleanupInterval_CanBeSetToCustomValue()
    {
        // Arrange
        var options = new HttpCacheOptions();
        var customInterval = TimeSpan.FromSeconds(30);

        // Act
        options.CleanupInterval = customInterval;

        // Assert
        options.CleanupInterval.Should().Be(customInterval);
    }
}
