using FluentAssertions;
using Ghost.Sdk.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class MemoryUsageExtensionTests
{
    private readonly ILogger<MemoryUsageExtension> _logger;

    public MemoryUsageExtensionTests()
    {
        _logger = Substitute.For<ILogger<MemoryUsageExtension>>();
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange
        var options = new MemoryOptions { MaxMemoryBytes = 1024 };

        // Act
        var extension = new MemoryUsageExtension(_logger, options);

        // Assert
        extension.Should().NotBeNull();
        extension.MaxMemoryBytes.Should().Be(1024);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = new MemoryOptions();

        // Act
        var act = () => new MemoryUsageExtension(null!, options);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new MemoryUsageExtension(_logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("options");
    }

    [Fact]
    public void Constructor_WithDefaultOptions_UsesDefaultMaxMemory()
    {
        // Act
        var extension = new MemoryUsageExtension(_logger);

        // Assert
        extension.MaxMemoryBytes.Should().Be(512 * 1024 * 1024); // 512 MB default
    }

    [Fact]
    public async Task CheckMemoryAsync_WhenMemoryBelowLimit_ReturnsTrue()
    {
        // Arrange
        var options = new MemoryOptions
        {
            MaxMemoryBytes = 10L * 1024 * 1024 * 1024 // 10 GB - well above current usage
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var result = await extension.CheckMemoryAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckMemoryAsync_WhenMemoryExceedsLimit_ReturnsFalse()
    {
        // Arrange
        var options = new MemoryOptions
        {
            MaxMemoryBytes = 1 // 1 byte - guaranteed to exceed
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var result = await extension.CheckMemoryAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckMemoryAsync_WithNoLimit_ReturnsTrue()
    {
        // Arrange
        var options = new MemoryOptions
        {
            MaxMemoryBytes = 0 // No limit
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var result = await extension.CheckMemoryAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckMemoryAsync_UpdatesPeakMemory()
    {
        // Arrange
        var extension = new MemoryUsageExtension(_logger);

        // Act
        await extension.CheckMemoryAsync();
        var stats = extension.GetStats();

        // Assert
        stats.PeakBytes.Should().BeGreaterThan(0);
        stats.CurrentBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task CheckMemoryAsync_PeakMemoryNeverDecreases()
    {
        // Arrange
        var extension = new MemoryUsageExtension(_logger);

        // Act
        await extension.CheckMemoryAsync();
        var firstPeak = extension.GetStats().PeakBytes;

        await extension.CheckMemoryAsync();
        var secondPeak = extension.GetStats().PeakBytes;

        // Assert
        secondPeak.Should().BeGreaterOrEqualTo(firstPeak);
    }

    [Fact]
    public void GetStats_ReturnsCurrentMemoryInformation()
    {
        // Arrange
        var options = new MemoryOptions { MaxMemoryBytes = 1024 * 1024 };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var stats = extension.GetStats();

        // Assert
        stats.Should().NotBeNull();
        stats.CurrentBytes.Should().BeGreaterThan(0);
        stats.MaxAllowedBytes.Should().Be(1024 * 1024);
    }

    [Fact]
    public void GetStats_UsagePercent_CalculatesCorrectly()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new MemoryOptions
        {
            MaxMemoryBytes = currentMemory * 2 // Set limit at 2x current
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var stats = extension.GetStats();

        // Assert
        stats.UsagePercent.Should().BeGreaterThan(0);
        stats.UsagePercent.Should().BeLessThan(100);
    }

    [Fact]
    public void GetStats_WithNoLimit_ReturnsZeroPercent()
    {
        // Arrange
        var options = new MemoryOptions { MaxMemoryBytes = 0 };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var stats = extension.GetStats();

        // Assert
        stats.UsagePercent.Should().Be(0);
    }

    [Fact]
    public void MaxMemoryBytes_CanBeModified()
    {
        // Arrange
        var extension = new MemoryUsageExtension(_logger);
        var newLimit = 256L * 1024 * 1024;

        // Act
        extension.MaxMemoryBytes = newLimit;

        // Assert
        extension.MaxMemoryBytes.Should().Be(newLimit);
    }

    [Fact]
    public async Task CheckMemoryAsync_WithCancellationToken_Succeeds()
    {
        // Arrange
        var extension = new MemoryUsageExtension(_logger);
        using var cts = new CancellationTokenSource();

        // Act
        var result = await extension.CheckMemoryAsync(cts.Token);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task CheckMemoryAsync_MultipleCalls_ConsistentBehavior()
    {
        // Arrange
        var options = new MemoryOptions
        {
            MaxMemoryBytes = 10L * 1024 * 1024 * 1024 // 10 GB
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var result1 = await extension.CheckMemoryAsync();
        var result2 = await extension.CheckMemoryAsync();
        var result3 = await extension.CheckMemoryAsync();

        // Assert
        result1.Should().BeTrue();
        result2.Should().BeTrue();
        result3.Should().BeTrue();
    }
}
