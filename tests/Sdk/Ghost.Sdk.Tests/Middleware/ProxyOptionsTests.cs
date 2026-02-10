using FluentAssertions;
using Ghost.Sdk.Middleware;
using Xunit;

namespace Ghost.Sdk.Tests.Middleware;

public sealed class ProxyOptionsTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_ShouldSetDefaultValues()
    {
        // Act
        var options = new ProxyOptions();

        // Assert
        options.MaxFailures.Should().Be(3);
        options.RetryAfter.Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void MaxFailures_CanBeCustomized()
    {
        // Arrange
        var options = new ProxyOptions();

        // Act
        options.MaxFailures = 5;

        // Assert
        options.MaxFailures.Should().Be(5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RetryAfter_CanBeCustomized()
    {
        // Arrange
        var options = new ProxyOptions();

        // Act
        options.RetryAfter = TimeSpan.FromMinutes(10);

        // Assert
        options.RetryAfter.Should().Be(TimeSpan.FromMinutes(10));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Options_SupportsZeroMaxFailures()
    {
        // Arrange
        var options = new ProxyOptions();

        // Act
        options.MaxFailures = 0;

        // Assert
        options.MaxFailures.Should().Be(0);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Options_SupportsZeroRetryAfter()
    {
        // Arrange
        var options = new ProxyOptions();

        // Act
        options.RetryAfter = TimeSpan.Zero;

        // Assert
        options.RetryAfter.Should().Be(TimeSpan.Zero);
    }
}
