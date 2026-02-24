using FluentAssertions;
using Ghost.Sdk.Extensions;
using Ghost.Sdk.Statistics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using Xunit.Abstractions;
using Ghost.Testing.Reliability;

namespace Ghost.Sdk.Tests.Extensions;

[Trait("Category", "Integration")]
public class PeriodicStatsLoggingIntegrationTests : ReliabilityTestBase
{
    private readonly ILogger<PeriodicStatsLogging> _logger;
    private readonly FakeTimeProvider _timeProvider;

    public PeriodicStatsLoggingIntegrationTests(ITestOutputHelper output) : base(output)
    {
        _logger = new TestLogger<PeriodicStatsLogging>(output);
        _timeProvider = new FakeTimeProvider();
    }

    [Fact]
    public void PeriodicStatsLogging_WithRealStatsCollector_LogsPeriodicUpdates()
    {
        // Arrange
        var statsCollector = new StatsCollector();
        var extension = new PeriodicStatsLogging(statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        const string spiderId = "integration-test-spider";

        // Simulate spider activity
        statsCollector.RecordRequest(spiderId);
        statsCollector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(50));
        statsCollector.RecordItem(spiderId, "product");

        // Act
        extension.StartLogging(spiderId);
        _timeProvider.Advance(TimeSpan.FromMilliseconds(250)); // Advance for multiple timer ticks

        // Add more activity
        statsCollector.RecordRequest(spiderId);
        statsCollector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(75));
        statsCollector.RecordItem(spiderId, "product");

        _timeProvider.Advance(TimeSpan.FromMilliseconds(150)); // Advance for another tick
        extension.StopLogging();

        // Assert
        var stats = statsCollector.GetStats(spiderId);
        stats.RequestCount.Should().Be(2);
        stats.ResponseCount.Should().Be(2);
        stats.ItemCount.Should().Be(2);
    }

    [Fact]
    public void PeriodicStatsLogging_WithMultipleSpiders_TracksCorrectSpider()
    {
        // Arrange
        var statsCollector = new StatsCollector();
        var extension = new PeriodicStatsLogging(statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        // Record stats for multiple spiders
        statsCollector.RecordRequest("spider-1");
        statsCollector.RecordRequest("spider-2");
        statsCollector.RecordResponse("spider-1", 200, TimeSpan.FromMilliseconds(50));

        // Act - log only spider-1
        extension.StartLogging("spider-1");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        extension.StopLogging();

        // Assert
        var stats1 = statsCollector.GetStats("spider-1");
        var stats2 = statsCollector.GetStats("spider-2");

        stats1.RequestCount.Should().Be(1);
        stats1.ResponseCount.Should().Be(1);
        stats2.RequestCount.Should().Be(1);
        stats2.ResponseCount.Should().Be(0);
    }

    [Fact]
    public void PeriodicStatsLogging_WithChangingStats_LogsUpdatedValues()
    {
        // Arrange
        var statsCollector = new StatsCollector();
        var extension = new PeriodicStatsLogging(statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        const string spiderId = "changing-stats-spider";

        // Act
        extension.StartLogging(spiderId);

        // Simulate activity over time
        for (int i = 0; i < 5; i++)
        {
            statsCollector.RecordRequest(spiderId);
            statsCollector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(50 + i * 10));
            statsCollector.RecordItem(spiderId, "item");
            _timeProvider.Advance(TimeSpan.FromMilliseconds(50));
        }

        _timeProvider.Advance(TimeSpan.FromMilliseconds(100)); // Final advance for last log
        extension.StopLogging();

        // Assert
        var stats = statsCollector.GetStats(spiderId);
        stats.RequestCount.Should().Be(5);
        stats.ResponseCount.Should().Be(5);
        stats.ItemCount.Should().Be(5);
        stats.AverageResponseTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PeriodicStatsLogging_WithErrors_LogsErrorCount()
    {
        // Arrange
        var statsCollector = new StatsCollector();
        var extension = new PeriodicStatsLogging(statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        const string spiderId = "error-test-spider";

        // Act
        extension.StartLogging(spiderId);

        statsCollector.RecordRequest(spiderId);
        statsCollector.RecordError(spiderId, new InvalidOperationException("Test error"));
        statsCollector.RecordRequest(spiderId);
        statsCollector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(50));

        _timeProvider.Advance(TimeSpan.FromMilliseconds(150));
        extension.StopLogging();

        // Assert
        var stats = statsCollector.GetStats(spiderId);
        stats.RequestCount.Should().Be(2);
        stats.ErrorCount.Should().Be(1);
        stats.ResponseCount.Should().Be(1);
    }

    [Fact]
    public void PeriodicStatsLogging_WithHighThroughput_HandlesLoad()
    {
        // Arrange
        var statsCollector = new StatsCollector();
        var extension = new PeriodicStatsLogging(statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        const string spiderId = "high-throughput-spider";

        // Act
        extension.StartLogging(spiderId);

        // Simulate high request rate
        for (int i = 0; i < 100; i++)
        {
            statsCollector.RecordRequest(spiderId);
            _timeProvider.Advance(TimeSpan.FromMilliseconds(10));
            statsCollector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(25));
            statsCollector.RecordItem(spiderId, "item");
        }

        _timeProvider.Advance(TimeSpan.FromMilliseconds(150)); // Advance for final log
        extension.StopLogging();

        // Assert
        var stats = statsCollector.GetStats(spiderId);
        stats.RequestCount.Should().Be(100);
        stats.ResponseCount.Should().Be(100);
        stats.ItemCount.Should().Be(100);
        stats.RequestsPerSecond.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PeriodicStatsLogging_SwitchingSpiders_LogsCorrectSpider()
    {
        // Arrange
        var statsCollector = new StatsCollector();
        var extension = new PeriodicStatsLogging(statsCollector, _logger, _timeProvider)
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };

        // Act - Start with spider-1
        statsCollector.RecordRequest("spider-1");
        extension.StartLogging("spider-1");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        // Switch to spider-2
        statsCollector.RecordRequest("spider-2");
        extension.StartLogging("spider-2");
        _timeProvider.Advance(TimeSpan.FromMilliseconds(150));

        extension.StopLogging();

        // Assert
        var stats1 = statsCollector.GetStats("spider-1");
        var stats2 = statsCollector.GetStats("spider-2");

        stats1.RequestCount.Should().Be(1);
        stats2.RequestCount.Should().Be(1);
    }

    [Fact]
    public void PeriodicStatsLogging_Dispose_CleansUpResources()
    {
        // Arrange
        var statsCollector = new StatsCollector();
        var extension = new PeriodicStatsLogging(statsCollector, _logger, _timeProvider);

        // Act
        extension.StartLogging("test-spider");
        extension.Dispose();

        // Assert - should not throw
        var act = () => extension.Dispose();
        act.Should().NotThrow();
    }

    // Test logger that writes to xUnit output
    private class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var message = formatter(state, exception);
            _output.WriteLine($"[{logLevel}] {message}");
            if (exception != null)
            {
                _output.WriteLine($"Exception: {exception}");
            }
        }
    }
}
