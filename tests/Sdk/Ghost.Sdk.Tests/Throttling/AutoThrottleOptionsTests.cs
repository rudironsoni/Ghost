using FluentAssertions;
using Ghost.Sdk.Throttling;
using Xunit;

namespace Ghost.Sdk.Tests.Throttling;

[Trait("Category", "Unit")]
public class AutoThrottleOptionsTests
{
    [Fact]
    public void Constructor_WithDefaultValues_SetsExpectedDefaults()
    {
        // Arrange & Act
        var options = new AutoThrottleOptions();

        // Assert
        options.StartDelay.Should().Be(1.0);
        options.MinDelay.Should().Be(0.1);
        options.MaxDelay.Should().Be(60.0);
        options.TargetLatency.Should().Be(TimeSpan.FromSeconds(1));
        options.MaxSamples.Should().Be(100);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var options = new AutoThrottleOptions
        {
            StartDelay = 2.0,
            MinDelay = 0.5,
            MaxDelay = 30.0,
            TargetLatency = TimeSpan.FromSeconds(2),
            MaxSamples = 50
        };

        // Assert
        options.StartDelay.Should().Be(2.0);
        options.MinDelay.Should().Be(0.5);
        options.MaxDelay.Should().Be(30.0);
        options.TargetLatency.Should().Be(TimeSpan.FromSeconds(2));
        options.MaxSamples.Should().Be(50);
    }
}
