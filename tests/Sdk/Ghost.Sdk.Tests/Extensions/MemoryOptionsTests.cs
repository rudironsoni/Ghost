using FluentAssertions;
using Ghost.Sdk.Extensions;
using Xunit;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class MemoryOptionsTests
{
    [Fact]
    public void Constructor_UsesDefaultMaxMemoryBytes()
    {
        // Act
        var options = new MemoryOptions();

        // Assert
        options.MaxMemoryBytes.Should().Be(512 * 1024 * 1024); // 512 MB
    }

    [Fact]
    public void Constructor_UsesDefaultWarningThreshold()
    {
        // Act
        var options = new MemoryOptions();

        // Assert
        options.WarningThresholdPercent.Should().Be(80);
    }

    [Fact]
    public void Constructor_UsesDefaultEnableGarbageCollection()
    {
        // Act
        var options = new MemoryOptions();

        // Assert
        options.EnableGarbageCollection.Should().BeTrue();
    }

    [Fact]
    public void MaxMemoryBytes_CanBeModified()
    {
        // Arrange
        var options = new MemoryOptions();
        var newValue = 1024L * 1024 * 1024;

        // Act
        options.MaxMemoryBytes = newValue;

        // Assert
        options.MaxMemoryBytes.Should().Be(newValue);
    }

    [Fact]
    public void WarningThresholdPercent_CanBeModified()
    {
        // Arrange
        var options = new MemoryOptions();

        // Act
        options.WarningThresholdPercent = 90;

        // Assert
        options.WarningThresholdPercent.Should().Be(90);
    }

    [Fact]
    public void EnableGarbageCollection_CanBeModified()
    {
        // Arrange
        var options = new MemoryOptions();

        // Act
        options.EnableGarbageCollection = false;

        // Assert
        options.EnableGarbageCollection.Should().BeFalse();
    }

    [Fact]
    public void Properties_CanBeSetThroughInitializer()
    {
        // Act
        var options = new MemoryOptions
        {
            MaxMemoryBytes = 256 * 1024 * 1024,
            WarningThresholdPercent = 75,
            EnableGarbageCollection = false
        };

        // Assert
        options.MaxMemoryBytes.Should().Be(256 * 1024 * 1024);
        options.WarningThresholdPercent.Should().Be(75);
        options.EnableGarbageCollection.Should().BeFalse();
    }
}
