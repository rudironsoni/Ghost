using FluentAssertions;
using Ghost.Sdk.Extensions;
using Ghost.Sdk.Spider.Contracts;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class MaxDurationConditionTests : ReliabilityTestBase
{
    public MaxDurationConditionTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task IsMetAsync_WhenDurationBelowMax_ReturnsFalse()
    {
        // Arrange
        var condition = new MaxDurationCondition(TimeSpan.FromMinutes(5));
        var context = new SpiderContext
        {
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-2)
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMetAsync_WhenDurationExceedsMax_ReturnsTrue()
    {
        // Arrange
        var condition = new MaxDurationCondition(TimeSpan.FromMinutes(5));
        var context = new SpiderContext
        {
            StartTime = DateTimeOffset.UtcNow.AddMinutes(-10)
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsMetAsync_WhenDurationApproximatelyEqualsMax_ReturnsTrue()
    {
        // Arrange
        var condition = new MaxDurationCondition(TimeSpan.FromSeconds(1));
        var context = new SpiderContext
        {
            StartTime = DateTimeOffset.UtcNow.AddSeconds(-1.1)
        };

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithZeroDuration_ThrowsArgumentException()
    {
        // Act
        var act = () => new MaxDurationCondition(TimeSpan.Zero);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxDuration");
    }

    [Fact]
    public void Constructor_WithNegativeDuration_ThrowsArgumentException()
    {
        // Act
        var act = () => new MaxDurationCondition(TimeSpan.FromMinutes(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxDuration");
    }

    [Fact]
    public async Task IsMetAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var condition = new MaxDurationCondition(TimeSpan.FromMinutes(5));

        // Act
        var act = async () => await condition.IsMetAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("context");
    }

    [Fact]
    public void Name_ReturnsDescriptiveName()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(5);
        var condition = new MaxDurationCondition(duration);

        // Act
        var name = condition.Name;

        // Assert
        name.Should().Be($"MaxDuration({duration})");
    }

    [Fact]
    public void MaxDuration_ReturnsConfiguredValue()
    {
        // Arrange
        var duration = TimeSpan.FromMinutes(5);
        var condition = new MaxDurationCondition(duration);

        // Act
        var maxDuration = condition.MaxDuration;

        // Assert
        maxDuration.Should().Be(duration);
    }
}
