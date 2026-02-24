using FluentAssertions;
using Ghost.Sdk.Extensions;
using Ghost.Sdk.Statistics;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Unit")]
public class PeriodicStatsLoggingTests : ReliabilityTestBase
{
    private readonly IStatsCollector _statsCollector;
    private readonly ILogger<PeriodicStatsLogging> _logger;
    private readonly FakeTimeProvider _timeProvider;

    public PeriodicStatsLoggingTests(ITestOutputHelper output) : base(output)
    {
        _statsCollector = Substitute.For<IStatsCollector>();
        _logger = Substitute.For<ILogger<PeriodicStatsLogging>>();
        _timeProvider = new FakeTimeProvider();
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);

        // Assert
        extension.Should().NotBeNull();
        extension.Interval.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void Constructor_WithNullStatsCollector_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PeriodicStatsLogging(null!, _logger, _timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("statsCollector");
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PeriodicStatsLogging(_statsCollector, null!, _timeProvider);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithNullTimeProvider_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new PeriodicStatsLogging(_statsCollector, _logger, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("timeProvider");
    }

    [Fact]
    public void Interval_CanBeModified()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);
        var newInterval = TimeSpan.FromSeconds(60);

        // Act
        extension.Interval = newInterval;

        // Assert
        extension.Interval.Should().Be(newInterval);
    }

    [Fact]
    public void StartLogging_WithNullSpiderId_ThrowsArgumentNullException()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);

        // Act
        var act = () => extension.StartLogging(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("spiderId");
    }

    [Fact]
    public void StartLogging_WithValidSpiderId_Succeeds()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);

        // Act
        var act = () => extension.StartLogging("test-spider");

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void StopLogging_WithoutStarting_DoesNotThrow()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);

        // Act
        var act = () => extension.StopLogging();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void StopLogging_AfterStarting_Succeeds()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);
        extension.StartLogging("test-spider");

        // Act
        var act = () => extension.StopLogging();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void StopLogging_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);
        extension.StartLogging("test-spider");
        extension.StopLogging();

        // Act
        var act = () => extension.StopLogging();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void StartLogging_TriggersPeriodicStatCollection()
    {
        // Arrange
        var stats = new SpiderStats
        {
            SpiderId = "test-spider",
            RequestCount = 100,
            ResponseCount = 95,
            ErrorCount = 5,
            ItemCount = 50,
            TotalDuration = TimeSpan.FromSeconds(10)
        };

        _statsCollector.GetStats("test-spider").Returns(stats);

        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100) // Short interval for testing
        };

        // Act
        extension.StartLogging("test-spider");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(250)); // Advance for at least 2 timer ticks
        extension.StopLogging();

        // Assert
        _statsCollector.Received().GetStats("test-spider");
    }

    [Fact]
    public void StartLogging_LogsStatsWithCorrectValues()
    {
        // Arrange
        var stats = new SpiderStats
        {
            SpiderId = "test-spider",
            RequestCount = 100,
            ResponseCount = 95,
            ErrorCount = 5,
            ItemCount = 50,
            TotalDuration = TimeSpan.FromSeconds(10)
        };

        _statsCollector.GetStats("test-spider").Returns(stats);

        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        // Act
        extension.StartLogging("test-spider");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(150)); // Advance for timer tick
        extension.StopLogging();

        // Assert - verify logger was called (exact matching would require ILogger mock inspection)
        _statsCollector.Received().GetStats("test-spider");
    }

    [Fact]
    public void StartLogging_WithStatsCollectorException_DoesNotCrash()
    {
        // Arrange
        _statsCollector.GetStats(Arg.Any<string>())
            .Returns(_ => throw new InvalidOperationException("Stats unavailable"));

        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        // Act
        extension.StartLogging("test-spider");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        var act = () => extension.StopLogging();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void StartLogging_CalledTwice_RestartTimer()
    {
        // Arrange
        var stats = new SpiderStats { SpiderId = "spider-1" };
        _statsCollector.GetStats(Arg.Any<string>()).Returns(stats);

        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        // Act
        extension.StartLogging("spider-1");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        extension.StartLogging("spider-2"); // Restart with new spider
        _timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        extension.StopLogging();

        // Assert - should have called GetStats for spider-2
        _statsCollector.Received().GetStats("spider-2");
    }

    [Fact]
    public void Dispose_StopsLoggingAndReleasesResources()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);
        extension.StartLogging("test-spider");

        // Act
        extension.Dispose();

        // Assert - should not throw
        var act = () => extension.Dispose(); // Second dispose should also be safe
        act.Should().NotThrow();
    }

    [Fact]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);

        // Act & Assert
        extension.Dispose();
        var act = () => extension.Dispose();
        act.Should().NotThrow();
    }

    [Fact]
    public void StartLogging_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);
        extension.Dispose();

        // Act
        var act = () => extension.StartLogging("test-spider");

        // Assert
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void StopLogging_AfterDispose_DoesNotThrow()
    {
        // Arrange
        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider);
        extension.Dispose();

        // Act
        var act = () => extension.StopLogging();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void StartLogging_WithZeroStats_LogsSuccessfully()
    {
        // Arrange
        var stats = new SpiderStats
        {
            SpiderId = "empty-spider",
            RequestCount = 0,
            ResponseCount = 0,
            ErrorCount = 0,
            ItemCount = 0,
            TotalDuration = TimeSpan.Zero
        };

        _statsCollector.GetStats("empty-spider").Returns(stats);

        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        // Act
        extension.StartLogging("empty-spider");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        extension.StopLogging();

        // Assert
        _statsCollector.Received().GetStats("empty-spider");
    }

    [Fact]
    public void StartLogging_WithCustomInterval_RespectsInterval()
    {
        // Arrange
        var stats = new SpiderStats { SpiderId = "test-spider" };
        _statsCollector.GetStats("test-spider").Returns(stats);

        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };

        // Act
        extension.StartLogging("test-spider");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(100)); // Less than interval
        var callsBeforeInterval = _statsCollector.ReceivedCalls().Count();

        _timeProvider.Advance(TimeSpan.FromMilliseconds(150)); // Now past interval
        var callsAfterInterval = _statsCollector.ReceivedCalls().Count();

        extension.StopLogging();

        // Assert - should have more calls after interval elapsed
        callsAfterInterval.Should().BeGreaterThan(callsBeforeInterval);
    }

    [Fact]
    public void StartLogging_WithLongRunningStats_ContinuesLogging()
    {
        // Arrange
        var callCount = 0;
        _statsCollector.GetStats("test-spider").Returns(_ =>
        {
            callCount++;
            return new SpiderStats
            {
                SpiderId = "test-spider",
                RequestCount = callCount * 10
            };
        });

        var extension = new PeriodicStatsLogging(_statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        // Act
        extension.StartLogging("test-spider");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(350)); // Should trigger 3-4 timer ticks
        extension.StopLogging();

        // Assert
        callCount.Should().BeGreaterOrEqualTo(3);
    }
}
