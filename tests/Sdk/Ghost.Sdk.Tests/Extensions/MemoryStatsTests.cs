using FluentAssertions;
using Ghost.Sdk.Extensions;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class MemoryStatsTests : ReliabilityTestBase
{
    public MemoryStatsTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void UsagePercent_WithValidValues_CalculatesCorrectly()
    {
        // Arrange
        var stats = new MemoryStats
        {
            CurrentBytes = 50 * 1024 * 1024, // 50 MB
            MaxAllowedBytes = 100 * 1024 * 1024 // 100 MB
        };

        // Act
        var percent = stats.UsagePercent;

        // Assert
        percent.Should().BeApproximately(50.0, 0.01);
    }

    [Fact]
    public void UsagePercent_WithZeroMax_ReturnsZero()
    {
        // Arrange
        var stats = new MemoryStats
        {
            CurrentBytes = 50 * 1024 * 1024,
            MaxAllowedBytes = 0
        };

        // Act
        var percent = stats.UsagePercent;

        // Assert
        percent.Should().Be(0);
    }

    [Fact]
    public void UsagePercent_WhenExceedingLimit_ReturnsOverHundred()
    {
        // Arrange
        var stats = new MemoryStats
        {
            CurrentBytes = 150 * 1024 * 1024, // 150 MB
            MaxAllowedBytes = 100 * 1024 * 1024 // 100 MB
        };

        // Act
        var percent = stats.UsagePercent;

        // Assert
        percent.Should().BeApproximately(150.0, 0.01);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var stats = new MemoryStats
        {
            CurrentBytes = 100,
            PeakBytes = 200,
            MaxAllowedBytes = 300
        };

        // Act & Assert
        stats.CurrentBytes.Should().Be(100);
        stats.PeakBytes.Should().Be(200);
        stats.MaxAllowedBytes.Should().Be(300);
    }

    [Fact]
    public void UsagePercent_WithZeroCurrent_ReturnsZero()
    {
        // Arrange
        var stats = new MemoryStats
        {
            CurrentBytes = 0,
            MaxAllowedBytes = 100 * 1024 * 1024
        };

        // Act
        var percent = stats.UsagePercent;

        // Assert
        percent.Should().Be(0);
    }

    [Fact]
    public void UsagePercent_AtExactLimit_ReturnsHundred()
    {
        // Arrange
        var stats = new MemoryStats
        {
            CurrentBytes = 100 * 1024 * 1024,
            MaxAllowedBytes = 100 * 1024 * 1024
        };

        // Act
        var percent = stats.UsagePercent;

        // Assert
        percent.Should().BeApproximately(100.0, 0.01);
    }
}
