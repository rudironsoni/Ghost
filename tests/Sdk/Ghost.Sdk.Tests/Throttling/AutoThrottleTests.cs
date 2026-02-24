using FluentAssertions;
using Ghost.Sdk.Throttling;
using Xunit;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Throttling;

[Trait("Category", "Unit")]
public class AutoThrottleTests : ReliabilityTestBase
{
    public AutoThrottleTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange & Act
        var act = () => new AutoThrottle(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNegativeMinDelay_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions { MinDelay = -1.0 };

        // Act
        var act = () => new AutoThrottle(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MinDelay*");
    }

    [Fact]
    public void Constructor_WithMaxDelayLessThanMinDelay_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            MinDelay = 10.0,
            MaxDelay = 5.0
        };

        // Act
        var act = () => new AutoThrottle(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxDelay*");
    }

    [Fact]
    public void Constructor_WithStartDelayBelowMinDelay_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            MinDelay = 2.0,
            StartDelay = 1.0,
            MaxDelay = 10.0
        };

        // Act
        var act = () => new AutoThrottle(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*StartDelay*");
    }

    [Fact]
    public void Constructor_WithStartDelayAboveMaxDelay_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            MinDelay = 0.1,
            StartDelay = 15.0,
            MaxDelay = 10.0
        };

        // Act
        var act = () => new AutoThrottle(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*StartDelay*");
    }

    [Fact]
    public void Constructor_WithZeroTargetLatency_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            TargetLatency = TimeSpan.Zero
        };

        // Act
        var act = () => new AutoThrottle(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TargetLatency*");
    }

    [Fact]
    public void Constructor_WithNegativeTargetLatency_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            TargetLatency = TimeSpan.FromSeconds(-1)
        };

        // Act
        var act = () => new AutoThrottle(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*TargetLatency*");
    }

    [Fact]
    public void Constructor_WithZeroMaxSamples_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            MaxSamples = 0
        };

        // Act
        var act = () => new AutoThrottle(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxSamples*");
    }

    [Fact]
    public void Constructor_WithNegativeMaxSamples_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            MaxSamples = -1
        };

        // Act
        var act = () => new AutoThrottle(options);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxSamples*");
    }

    [Fact]
    public async Task GetDelayAsync_BeforeAnyLatencyRecorded_ReturnsStartDelay()
    {
        // Arrange
        var options = new AutoThrottleOptions { StartDelay = 2.5 };
        var throttle = new AutoThrottle(options);

        // Act
        var delay = await throttle.GetDelayAsync();

        // Assert
        delay.Should().Be(2.5);
    }

    [Fact]
    public async Task RecordLatencyAsync_WithNegativeLatency_ThrowsArgumentException()
    {
        // Arrange
        var options = new AutoThrottleOptions();
        var throttle = new AutoThrottle(options);

        // Act
        var act = async () => await throttle.RecordLatencyAsync(TimeSpan.FromSeconds(-1));

        // Assert
        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Latency*");
    }

    [Fact]
    public async Task RecordLatencyAsync_WithLowLatency_DecreasesDelay()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 2.0,
            MinDelay = 0.1,
            TargetLatency = TimeSpan.FromSeconds(1)
        };
        var throttle = new AutoThrottle(options);
        var initialDelay = await throttle.GetDelayAsync();

        // Act - Record latencies well below target (< 80% of 1 second = < 800ms)
        for (var i = 0; i < 10; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(200));
        }

        var finalDelay = await throttle.GetDelayAsync();

        // Assert
        finalDelay.Should().BeLessThan(initialDelay);
        finalDelay.Should().BeGreaterOrEqualTo(options.MinDelay);
    }

    [Fact]
    public async Task RecordLatencyAsync_WithHighLatency_IncreasesDelay()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 1.0,
            MaxDelay = 60.0,
            TargetLatency = TimeSpan.FromSeconds(1)
        };
        var throttle = new AutoThrottle(options);
        var initialDelay = await throttle.GetDelayAsync();

        // Act - Record latencies well above target (> 120% of 1 second = > 1200ms)
        for (var i = 0; i < 10; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(2000));
        }

        var finalDelay = await throttle.GetDelayAsync();

        // Assert
        finalDelay.Should().BeGreaterThan(initialDelay);
        finalDelay.Should().BeLessOrEqualTo(options.MaxDelay);
    }

    [Fact]
    public async Task RecordLatencyAsync_WithLatencyNearTarget_MaintainsDelay()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 1.0,
            MinDelay = 0.1,
            MaxDelay = 60.0,
            TargetLatency = TimeSpan.FromSeconds(1)
        };
        var throttle = new AutoThrottle(options);
        var initialDelay = await throttle.GetDelayAsync();

        // Act - Record latencies within 80%-120% of target (800-1200ms)
        for (var i = 0; i < 10; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(1000));
        }

        var finalDelay = await throttle.GetDelayAsync();

        // Assert
        finalDelay.Should().Be(initialDelay);
    }

    [Fact]
    public async Task RecordLatencyAsync_RespectsMinDelayBound()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 1.0,
            MinDelay = 0.5,
            TargetLatency = TimeSpan.FromSeconds(1)
        };
        var throttle = new AutoThrottle(options);

        // Act - Record many very low latencies to try to push below MinDelay
        for (var i = 0; i < 100; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(1));
        }

        var finalDelay = await throttle.GetDelayAsync();

        // Assert
        finalDelay.Should().BeGreaterOrEqualTo(options.MinDelay);
    }

    [Fact]
    public async Task RecordLatencyAsync_RespectsMaxDelayBound()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 1.0,
            MaxDelay = 5.0,
            TargetLatency = TimeSpan.FromSeconds(1)
        };
        var throttle = new AutoThrottle(options);

        // Act - Record many very high latencies to try to push above MaxDelay
        for (var i = 0; i < 100; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.FromSeconds(10));
        }

        var finalDelay = await throttle.GetDelayAsync();

        // Assert
        finalDelay.Should().BeLessOrEqualTo(options.MaxDelay);
    }

    [Fact]
    public async Task RecordLatencyAsync_MaintainsSlidingWindow()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 1.0,
            MaxSamples = 5
        };
        var throttle = new AutoThrottle(options);

        // Act - Record more samples than MaxSamples
        for (var i = 0; i < 10; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(100));
        }

        // Assert
        throttle.SampleCount.Should().Be(5);
    }

    [Fact]
    public async Task SampleCount_InitiallyZero()
    {
        // Arrange
        var options = new AutoThrottleOptions();
        var throttle = new AutoThrottle(options);

        // Assert
        throttle.SampleCount.Should().Be(0);
    }

    [Fact]
    public async Task SampleCount_IncreasesWithRecordedLatencies()
    {
        // Arrange
        var options = new AutoThrottleOptions { MaxSamples = 10 };
        var throttle = new AutoThrottle(options);

        // Act
        await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(100));
        await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(200));
        await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(300));

        // Assert
        throttle.SampleCount.Should().Be(3);
    }

    [Fact]
    public async Task AverageLatency_InitiallyZero()
    {
        // Arrange
        var options = new AutoThrottleOptions();
        var throttle = new AutoThrottle(options);

        // Assert
        throttle.AverageLatency.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task AverageLatency_CalculatesCorrectAverage()
    {
        // Arrange
        var options = new AutoThrottleOptions();
        var throttle = new AutoThrottle(options);

        // Act
        await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(100));
        await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(200));
        await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(300));

        // Assert - Average should be 200ms
        throttle.AverageLatency.Should().BeCloseTo(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task GetDelayAsync_IsThreadSafe()
    {
        // Arrange
        var options = new AutoThrottleOptions();
        var throttle = new AutoThrottle(options);

        // Act - Call GetDelayAsync from multiple threads concurrently
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(async () => await throttle.GetDelayAsync()))
            .ToList();

        var delays = await Task.WhenAll(tasks);

        // Assert - All calls should succeed and return consistent values
        delays.Should().AllSatisfy(d => d.Should().BeGreaterOrEqualTo(0));
        delays.Distinct().Should().HaveCountLessOrEqualTo(2); // Should be same or very similar values
    }

    [Fact]
    public async Task RecordLatencyAsync_IsThreadSafe()
    {
        // Arrange
        var options = new AutoThrottleOptions { MaxSamples = 1000 };
        var throttle = new AutoThrottle(options);

        // Act - Record latencies from multiple threads concurrently
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(async () =>
                await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(100))))
            .ToList();

        await Task.WhenAll(tasks);

        // Assert - All samples should be recorded
        throttle.SampleCount.Should().Be(100);
    }

    [Fact]
    public async Task RecordLatencyAsync_WithMixedLatencies_ConvergesToStableValue()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 1.0,
            MinDelay = 0.1,
            MaxDelay = 10.0,
            TargetLatency = TimeSpan.FromSeconds(1)
        };
        var throttle = new AutoThrottle(options);

        // Act - Record alternating high and low latencies
        for (var i = 0; i < 20; i++)
        {
            var latency = i % 2 == 0
                ? TimeSpan.FromMilliseconds(200)  // Low
                : TimeSpan.FromMilliseconds(1800); // High
            await throttle.RecordLatencyAsync(latency);
        }

        var finalDelay = await throttle.GetDelayAsync();

        // Assert - Should stabilize somewhere in the middle range
        finalDelay.Should().BeInRange(options.MinDelay, options.MaxDelay);
    }

    [Fact]
    public async Task AutoThrottle_AdaptiveAlgorithm_DecreasesThenIncreases()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 2.0,
            MinDelay = 0.1,
            MaxDelay = 10.0,
            TargetLatency = TimeSpan.FromSeconds(1)
        };
        var throttle = new AutoThrottle(options);

        // Act - Phase 1: Record low latencies to decrease delay
        for (var i = 0; i < 10; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.FromMilliseconds(100));
        }
        var decreasedDelay = await throttle.GetDelayAsync();

        // Phase 2: Record high latencies to increase delay
        for (var i = 0; i < 10; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.FromSeconds(5));
        }
        var increasedDelay = await throttle.GetDelayAsync();

        // Assert
        decreasedDelay.Should().BeLessThan(options.StartDelay);
        increasedDelay.Should().BeGreaterThan(decreasedDelay);
    }

    [Fact]
    public async Task AutoThrottle_WithZeroLatencies_DecreasesDelay()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 2.0,
            MinDelay = 0.1,
            TargetLatency = TimeSpan.FromSeconds(1)
        };
        var throttle = new AutoThrottle(options);
        var initialDelay = await throttle.GetDelayAsync();

        // Act - Record zero latencies (extremely fast responses)
        for (var i = 0; i < 10; i++)
        {
            await throttle.RecordLatencyAsync(TimeSpan.Zero);
        }

        var finalDelay = await throttle.GetDelayAsync();

        // Assert
        finalDelay.Should().BeLessThan(initialDelay);
        finalDelay.Should().BeGreaterOrEqualTo(options.MinDelay);
    }
}
