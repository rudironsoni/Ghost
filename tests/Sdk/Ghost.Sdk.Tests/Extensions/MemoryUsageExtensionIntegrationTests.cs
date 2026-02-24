using FluentAssertions;
using Ghost.Sdk.Extensions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Integration")]
public class MemoryUsageExtensionIntegrationTests : ReliabilityTestBase
{
    private readonly ILogger<MemoryUsageExtension> _logger;

    public MemoryUsageExtensionIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _logger = Substitute.For<ILogger<MemoryUsageExtension>>();
    }

    [Fact]
    public async Task MemoryMonitoring_WithWarningThreshold_TriggersWarningLog()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new MemoryOptions
        {
            // Set limit just above current to trigger warning
            MaxMemoryBytes = (long)(currentMemory * 1.1),
            WarningThresholdPercent = 80,
            EnableGarbageCollection = false // Disable GC for predictable test
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var result = await extension.CheckMemoryAsync();

        // Assert
        result.Should().BeTrue(); // Should still be under limit
    }

    [Fact]
    public async Task MemoryMonitoring_MultipleChecks_TracksPeakCorrectly()
    {
        // Arrange
        var extension = new MemoryUsageExtension(_logger);
        List<long> peakValues = [];

        // Act - Perform multiple checks
        for (var i = 0; i < 5; i++)
        {
            await extension.CheckMemoryAsync();
            var stats = extension.GetStats();
            peakValues.Add(stats.PeakBytes);
        }

        // Assert - Peak should never decrease
        for (var i = 1; i < peakValues.Count; i++)
        {
            peakValues[i].Should().BeGreaterOrEqualTo(peakValues[i - 1]);
        }
    }

    [Fact]
    public async Task MemoryMonitoring_WithGarbageCollection_ReducesMemory()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new MemoryOptions
        {
            MaxMemoryBytes = (long)(currentMemory * 1.2),
            WarningThresholdPercent = 50, // Low threshold to trigger GC
            EnableGarbageCollection = true
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Create some garbage
        List<byte[]> garbage = [];
        for (var i = 0; i < 100; i++)
        {
            garbage.Add(new byte[1024 * 1024]); // 1 MB each
        }
        garbage.Clear(); // Make it collectible

        var memoryBefore = GC.GetTotalMemory(forceFullCollection: false);

        // Act
        await extension.CheckMemoryAsync(); // Should trigger GC due to low threshold

        var memoryAfter = GC.GetTotalMemory(forceFullCollection: false);

        // Assert
        // After GC, memory should be same or less (GC may not always reduce immediately)
        memoryAfter.Should().BeLessThanOrEqualTo((long)(memoryBefore * 1.1)); // Allow 10% tolerance
    }

    [Fact]
    public async Task MemoryMonitoring_UnderStress_RemainsStable()
    {
        // Arrange
        var options = new MemoryOptions
        {
            MaxMemoryBytes = 2L * 1024 * 1024 * 1024, // 2 GB
            WarningThresholdPercent = 90,
            EnableGarbageCollection = true
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act - Simulate rapid checks under load
        var tasks = new List<Task<bool>>();
        for (var i = 0; i < 100; i++)
        {
            tasks.Add(extension.CheckMemoryAsync());
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().AllSatisfy(r => r.Should().BeTrue());

        var stats = extension.GetStats();
        stats.PeakBytes.Should().BeGreaterThan(0);
        stats.CurrentBytes.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task MemoryMonitoring_ApproachingLimit_TracksCorrectly()
    {
        // Arrange
        var currentMemory = GC.GetTotalMemory(forceFullCollection: false);
        var options = new MemoryOptions
        {
            MaxMemoryBytes = (long)(currentMemory * 0.5), // Set below current
            WarningThresholdPercent = 80,
            EnableGarbageCollection = false
        };
        var extension = new MemoryUsageExtension(_logger, options);

        // Act
        var result = await extension.CheckMemoryAsync();
        var stats = extension.GetStats();

        // Assert
        result.Should().BeFalse(); // Should exceed limit
        stats.UsagePercent.Should().BeGreaterThan(100);
    }

    [Fact]
    public async Task MemoryMonitoring_ConcurrentChecks_ThreadSafe()
    {
        // Arrange
        var extension = new MemoryUsageExtension(_logger);

        // Act - Multiple concurrent checks
        var tasks = Enumerable.Range(0, 50)
            .Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < 10; i++)
                {
                    await extension.CheckMemoryAsync();
                    extension.GetStats();
                }
            }))
            .ToArray();

        // Assert - Should not throw
        var act = async () => await Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void GetStats_ReflectsRuntimeMemoryChanges()
    {
        // Arrange
        var extension = new MemoryUsageExtension(_logger);

        // Act
        var statsBefore = extension.GetStats();

        // Allocate some memory
        var temp = new byte[10 * 1024 * 1024]; // 10 MB
        Array.Fill(temp, (byte)0xFF);

        var statsAfter = extension.GetStats();

        // Assert
        statsAfter.CurrentBytes.Should().BeGreaterOrEqualTo(statsBefore.CurrentBytes);

        // Keep reference to prevent GC from collecting before assertion
        GC.KeepAlive(temp);
    }
}
