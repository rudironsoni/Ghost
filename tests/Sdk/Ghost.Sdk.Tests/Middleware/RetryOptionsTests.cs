using FluentAssertions;
using Ghost.Sdk.Middleware;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Middleware;

/// <summary>
/// Unit tests for RetryOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public class RetryOptionsTests : ReliabilityTestBase
{
    public RetryOptionsTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_SetsDefaultValues()
    {
        // Act
        var options = new RetryOptions();

        // Assert
        options.MaxRetries.Should().Be(3);
        options.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxDelay.Should().Be(TimeSpan.FromSeconds(30));
        options.BackoffMultiplier.Should().Be(2.0);
    }

    [Fact]
    public void MaxRetries_CanBeCustomized()
    {
        // Arrange
        var options = new RetryOptions();

        // Act
        options.MaxRetries = 5;

        // Assert
        options.MaxRetries.Should().Be(5);
    }

    [Fact]
    public void InitialDelay_CanBeCustomized()
    {
        // Arrange
        var options = new RetryOptions();
        var customDelay = TimeSpan.FromMilliseconds(500);

        // Act
        options.InitialDelay = customDelay;

        // Assert
        options.InitialDelay.Should().Be(customDelay);
    }

    [Fact]
    public void MaxDelay_CanBeCustomized()
    {
        // Arrange
        var options = new RetryOptions();
        var customMaxDelay = TimeSpan.FromMinutes(1);

        // Act
        options.MaxDelay = customMaxDelay;

        // Assert
        options.MaxDelay.Should().Be(customMaxDelay);
    }

    [Fact]
    public void BackoffMultiplier_CanBeCustomized()
    {
        // Arrange
        var options = new RetryOptions();

        // Act
        options.BackoffMultiplier = 1.5;

        // Assert
        options.BackoffMultiplier.Should().Be(1.5);
    }

    [Fact]
    public void ObjectInitializer_WorksCorrectly()
    {
        // Act
        var options = new RetryOptions
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromMilliseconds(200),
            MaxDelay = TimeSpan.FromMinutes(2),
            BackoffMultiplier = 3.0
        };

        // Assert
        options.MaxRetries.Should().Be(5);
        options.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(200));
        options.MaxDelay.Should().Be(TimeSpan.FromMinutes(2));
        options.BackoffMultiplier.Should().Be(3.0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(100)]
    public void MaxRetries_AcceptsValidValues(int maxRetries)
    {
        // Arrange
        var options = new RetryOptions();

        // Act
        options.MaxRetries = maxRetries;

        // Assert
        options.MaxRetries.Should().Be(maxRetries);
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    [InlineData(3.0)]
    [InlineData(10.0)]
    public void BackoffMultiplier_AcceptsValidValues(double multiplier)
    {
        // Arrange
        var options = new RetryOptions();

        // Act
        options.BackoffMultiplier = multiplier;

        // Assert
        options.BackoffMultiplier.Should().Be(multiplier);
    }
}
