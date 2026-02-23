using FluentAssertions;
using Ghost.Sdk.Spider.Contracts;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Ghost.Sdk.Spider.Tests.Contracts;

/// <summary>
/// Unit tests for <see cref="SpiderContext"/>.
/// </summary>
public class SpiderContextTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void SpiderId_DefaultValue_IsEmptyString()
    {
        // Arrange & Act
        var context = new SpiderContext();

        // Assert
        context.SpiderId.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void State_CanBeSet()
    {
        // Arrange
        var context = new SpiderContext();

        // Act
        context.State = SpiderState.Running;

        // Assert
        context.State.Should().Be(SpiderState.Running);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void RequestCount_CanBeSet()
    {
        // Arrange
        var context = new SpiderContext();

        // Act
        context.RequestCount = 42;

        // Assert
        context.RequestCount.Should().Be(42);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ResponseCount_CanBeSet()
    {
        // Arrange
        var context = new SpiderContext();

        // Act
        context.ResponseCount = 38;

        // Assert
        context.ResponseCount.Should().Be(38);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void ItemCount_CanBeSet()
    {
        // Arrange
        var context = new SpiderContext();

        // Act
        context.ItemCount = 100;

        // Assert
        context.ItemCount.Should().Be(100);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void StartTime_CanBeSet()
    {
        // Arrange
        var context = new SpiderContext();
        var startTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        // Act
        context.StartTime = startTime;

        // Assert
        context.StartTime.Should().Be(startTime);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Duration_CalculatesCorrectTimeSpan()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow.AddMinutes(-5);
        var context = new SpiderContext
        {
            StartTime = startTime
        };

        // Act
        var duration = context.Duration;

        // Assert
        duration.Should().BeCloseTo(TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(1));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Duration_WithFutureStartTime_ReturnsNegativeTimeSpan()
    {
        // Arrange
        var startTime = DateTimeOffset.UtcNow.AddMinutes(5);
        var context = new SpiderContext
        {
            StartTime = startTime
        };

        // Act
        var duration = context.Duration;

        // Assert
        duration.Should().BeNegative();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Duration_UpdatesOverTime()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var context = new SpiderContext(timeProvider);

        var firstDuration = context.Duration;

        // Act - Advance time using FakeTimeProvider
        timeProvider.Advance(TimeSpan.FromMilliseconds(100));
        var secondDuration = context.Duration;

        // Assert
        secondDuration.Should().BeGreaterThan(firstDuration);
    }
}
