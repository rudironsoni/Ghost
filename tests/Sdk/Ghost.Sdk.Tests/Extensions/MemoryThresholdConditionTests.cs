using FluentAssertions;
using Ghost.Sdk.Extensions;
using Ghost.Sdk.Spider.Contracts;
using Xunit;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class MemoryThresholdConditionTests
{
    [Fact]
    public async Task IsMetAsync_WhenMemoryBelowThreshold_ReturnsFalse()
    {
        // Arrange
        // Set threshold well above current memory usage (e.g., 10 GB)
        var condition = new MemoryThresholdCondition(10L * 1024 * 1024 * 1024);
        var context = new SpiderContext();

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsMetAsync_WhenMemoryExceedsThreshold_ReturnsTrue()
    {
        // Arrange
        // Set threshold very low (1 byte) to ensure it's exceeded
        var condition = new MemoryThresholdCondition(1);
        var context = new SpiderContext();

        // Act
        var result = await condition.IsMetAsync(context);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithZeroMaxMemory_ThrowsArgumentException()
    {
        // Act
        var act = () => new MemoryThresholdCondition(0);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxMemoryBytes");
    }

    [Fact]
    public void Constructor_WithNegativeMaxMemory_ThrowsArgumentException()
    {
        // Act
        var act = () => new MemoryThresholdCondition(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxMemoryBytes");
    }

    [Fact]
    public async Task IsMetAsync_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        var condition = new MemoryThresholdCondition(1024 * 1024);

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
        var condition = new MemoryThresholdCondition(100 * 1024 * 1024); // 100 MB

        // Act
        var name = condition.Name;

        // Assert
        name.Should().Be("MemoryThreshold(100.00 MB)");
    }

    [Fact]
    public void MaxMemoryBytes_ReturnsConfiguredValue()
    {
        // Arrange
        var bytes = 100L * 1024 * 1024;
        var condition = new MemoryThresholdCondition(bytes);

        // Act
        var maxMemory = condition.MaxMemoryBytes;

        // Assert
        maxMemory.Should().Be(bytes);
    }

    [Fact]
    public void MaxMemoryMB_ReturnsConfiguredValueInMegabytes()
    {
        // Arrange
        var bytes = 100L * 1024 * 1024; // 100 MB
        var condition = new MemoryThresholdCondition(bytes);

        // Act
        var maxMemoryMB = condition.MaxMemoryMB;

        // Assert
        maxMemoryMB.Should().BeApproximately(100.0, 0.01);
    }

    [Fact]
    public async Task IsMetAsync_ConsecutiveCalls_ReturnsConsistentResults()
    {
        // Arrange
        var condition = new MemoryThresholdCondition(1); // Very low threshold
        var context = new SpiderContext();

        // Act
        var result1 = await condition.IsMetAsync(context);
        var result2 = await condition.IsMetAsync(context);

        // Assert
        result1.Should().Be(result2);
    }
}
