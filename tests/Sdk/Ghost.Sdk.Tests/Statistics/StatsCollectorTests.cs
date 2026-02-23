using FluentAssertions;
using Ghost.Sdk.Statistics;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Ghost.Sdk.Tests.Statistics;

[Trait("Category", "Unit")]
public class StatsCollectorTests
{
    [Fact]
    public void RecordRequest_WithNullSpiderId_ThrowsArgumentNullException()
    {
        // Arrange
        var collector = new StatsCollector();

        // Act
        var act = () => collector.RecordRequest(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordRequest_WithValidSpiderId_InitializesStats()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";

        // Act
        collector.RecordRequest(spiderId);
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.SpiderId.Should().Be(spiderId);
        stats.RequestCount.Should().Be(1);
        stats.StartTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void RecordRequest_CalledMultipleTimes_IncrementsCount()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";

        // Act
        collector.RecordRequest(spiderId);
        collector.RecordRequest(spiderId);
        collector.RecordRequest(spiderId);
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.RequestCount.Should().Be(3);
    }

    [Fact]
    public void RecordResponse_WithNullSpiderId_ThrowsArgumentNullException()
    {
        // Arrange
        var collector = new StatsCollector();

        // Act
        var act = () => collector.RecordResponse(null!, 200, TimeSpan.FromMilliseconds(100));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordResponse_WithUninitializedSpider_ThrowsInvalidOperationException()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";

        // Act
        var act = () => collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(100));

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not been initialized*");
    }

    [Fact]
    public void RecordResponse_WithValidData_UpdatesStats()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);

        // Act
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(150));
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.ResponseCount.Should().Be(1);
        stats.AverageResponseTime.Should().Be(150);
        stats.StatusCodeDistribution.Should().ContainKey(200).WhoseValue.Should().Be(1);
        stats.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void RecordResponse_WithMultipleResponses_CalculatesAverageLatency()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);

        // Act
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(100));
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(200));
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(300));
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.ResponseCount.Should().Be(3);
        stats.AverageResponseTime.Should().Be(200); // (100 + 200 + 300) / 3 = 200
    }

    [Fact]
    public void RecordResponse_WithDifferentStatusCodes_TracksDistribution()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);

        // Act
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(100));
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(100));
        collector.RecordResponse(spiderId, 404, TimeSpan.FromMilliseconds(100));
        collector.RecordResponse(spiderId, 500, TimeSpan.FromMilliseconds(100));
        collector.RecordResponse(spiderId, 500, TimeSpan.FromMilliseconds(100));
        collector.RecordResponse(spiderId, 500, TimeSpan.FromMilliseconds(100));
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.StatusCodeDistribution.Should().ContainKey(200).WhoseValue.Should().Be(2);
        stats.StatusCodeDistribution.Should().ContainKey(404).WhoseValue.Should().Be(1);
        stats.StatusCodeDistribution.Should().ContainKey(500).WhoseValue.Should().Be(3);
    }

    [Fact]
    public void RecordError_WithNullSpiderId_ThrowsArgumentNullException()
    {
        // Arrange
        var collector = new StatsCollector();

        // Act
        var act = () => collector.RecordError(null!, new Exception());

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordError_WithNullError_ThrowsArgumentNullException()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);

        // Act
        var act = () => collector.RecordError(spiderId, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordError_WithUninitializedSpider_ThrowsInvalidOperationException()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";

        // Act
        var act = () => collector.RecordError(spiderId, new Exception());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not been initialized*");
    }

    [Fact]
    public void RecordError_WithValidData_IncrementsErrorCount()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);

        // Act
        collector.RecordError(spiderId, new Exception("Test error"));
        collector.RecordError(spiderId, new InvalidOperationException("Another error"));
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.ErrorCount.Should().Be(2);
    }

    [Fact]
    public void RecordItem_WithNullSpiderId_ThrowsArgumentNullException()
    {
        // Arrange
        var collector = new StatsCollector();

        // Act
        var act = () => collector.RecordItem(null!, "product");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordItem_WithNullItemType_ThrowsArgumentNullException()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);

        // Act
        var act = () => collector.RecordItem(spiderId, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RecordItem_WithUninitializedSpider_ThrowsInvalidOperationException()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";

        // Act
        var act = () => collector.RecordItem(spiderId, "product");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*not been initialized*");
    }

    [Fact]
    public void RecordItem_WithValidData_IncrementsItemCount()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);

        // Act
        collector.RecordItem(spiderId, "product");
        collector.RecordItem(spiderId, "product");
        collector.RecordItem(spiderId, "review");
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.ItemCount.Should().Be(3);
    }

    [Fact]
    public void GetStats_WithNullSpiderId_ThrowsArgumentNullException()
    {
        // Arrange
        var collector = new StatsCollector();

        // Act
        var act = () => collector.GetStats(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetStats_WithUntrackedSpider_ReturnsEmptyStats()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "unknown-spider";

        // Act
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.Should().NotBeNull();
        stats.SpiderId.Should().Be(spiderId);
        stats.RequestCount.Should().Be(0);
        stats.ResponseCount.Should().Be(0);
        stats.ErrorCount.Should().Be(0);
        stats.ItemCount.Should().Be(0);
    }

    [Fact]
    public void GetAllStats_WithNoSpiders_ReturnsEmptyDictionary()
    {
        // Arrange
        var collector = new StatsCollector();

        // Act
        var allStats = collector.GetAllStats();

        // Assert
        allStats.Should().NotBeNull();
        allStats.Should().BeEmpty();
    }

    [Fact]
    public void GetAllStats_WithMultipleSpiders_ReturnsAllStats()
    {
        // Arrange
        var collector = new StatsCollector();
        collector.RecordRequest("spider-1");
        collector.RecordRequest("spider-2");
        collector.RecordRequest("spider-3");

        // Act
        var allStats = collector.GetAllStats();

        // Assert
        allStats.Should().HaveCount(3);
        allStats.Should().ContainKey("spider-1");
        allStats.Should().ContainKey("spider-2");
        allStats.Should().ContainKey("spider-3");
    }

    [Fact]
    public void SpiderStats_RequestsPerSecond_CalculatesCorrectly()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var collector = new StatsCollector(timeProvider);
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);

        // Act - Simulate some time passing using FakeTimeProvider
        timeProvider.Advance(TimeSpan.FromSeconds(1)); // Advance 1 second
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(100));
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.RequestsPerSecond.Should().BeGreaterThan(0);
        stats.RequestsPerSecond.Should().BeApproximately(1.0, 0.1); // ~1 request in ~1 second
    }

    [Fact]
    public void SpiderStats_RequestsPerSecond_WithZeroDuration_ReturnsZero()
    {
        // Arrange
        var stats = new SpiderStats
        {
            RequestCount = 10,
            TotalDuration = TimeSpan.Zero
        };

        // Act
        var rps = stats.RequestsPerSecond;

        // Assert
        rps.Should().Be(0);
    }

    [Fact]
    public void StatsCollector_IsThreadSafe_ForConcurrentRequests()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        var iterations = 100;

        // Act - Record requests from multiple threads concurrently
        var tasks = Enumerable.Range(0, iterations)
            .Select(_ => Task.Run(() => collector.RecordRequest(spiderId)))
            .ToList();

        Task.WaitAll(tasks.ToArray());
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.RequestCount.Should().Be(iterations);
    }

    [Fact]
    public void StatsCollector_IsThreadSafe_ForConcurrentResponses()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);
        var iterations = 100;

        // Act - Record responses from multiple threads concurrently
        var tasks = Enumerable.Range(0, iterations)
            .Select(_ => Task.Run(() =>
                collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(100))))
            .ToList();

        Task.WaitAll(tasks.ToArray());
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.ResponseCount.Should().Be(iterations);
        stats.StatusCodeDistribution[200].Should().Be(iterations);
    }

    [Fact]
    public void StatsCollector_IsThreadSafe_ForConcurrentErrors()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);
        var iterations = 100;

        // Act - Record errors from multiple threads concurrently
        var tasks = Enumerable.Range(0, iterations)
            .Select(_ => Task.Run(() =>
                collector.RecordError(spiderId, new Exception("Test"))))
            .ToList();

        Task.WaitAll(tasks.ToArray());
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.ErrorCount.Should().Be(iterations);
    }

    [Fact]
    public void StatsCollector_IsThreadSafe_ForConcurrentItems()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";
        collector.RecordRequest(spiderId);
        var iterations = 100;

        // Act - Record items from multiple threads concurrently
        var tasks = Enumerable.Range(0, iterations)
            .Select(_ => Task.Run(() =>
                collector.RecordItem(spiderId, "product")))
            .ToList();

        Task.WaitAll(tasks.ToArray());
        var stats = collector.GetStats(spiderId);

        // Assert
        stats.ItemCount.Should().Be(iterations);
    }

    [Fact]
    public void StatsCollector_IsThreadSafe_ForMultipleSpidersConcurrently()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderIds = Enumerable.Range(1, 10).Select(i => $"spider-{i}").ToList();
        var iterationsPerSpider = 50;

        // Act - Record operations for multiple spiders concurrently
        var tasks = spiderIds.SelectMany(spiderId =>
            Enumerable.Range(0, iterationsPerSpider)
                .Select(_ => Task.Run(() =>
                {
                    collector.RecordRequest(spiderId);
                    collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(100));
                    collector.RecordItem(spiderId, "product");
                }))
        ).ToList();

        Task.WaitAll(tasks.ToArray());

        // Assert
        var allStats = collector.GetAllStats();
        allStats.Should().HaveCount(10);
        foreach (var spiderId in spiderIds)
        {
            var stats = allStats[spiderId];
            stats.RequestCount.Should().Be(iterationsPerSpider);
            stats.ResponseCount.Should().Be(iterationsPerSpider);
            stats.ItemCount.Should().Be(iterationsPerSpider);
        }
    }

    [Fact]
    public void StatsCollector_CompleteWorkflow_TracksAllMetrics()
    {
        // Arrange
        var collector = new StatsCollector();
        var spiderId = "spider-1";

        // Act - Simulate complete spider workflow
        collector.RecordRequest(spiderId); // Request 1
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(100));
        collector.RecordItem(spiderId, "product");

        collector.RecordRequest(spiderId); // Request 2
        collector.RecordResponse(spiderId, 200, TimeSpan.FromMilliseconds(150));
        collector.RecordItem(spiderId, "product");

        collector.RecordRequest(spiderId); // Request 3
        collector.RecordResponse(spiderId, 404, TimeSpan.FromMilliseconds(50));
        // No item for 404

        collector.RecordRequest(spiderId); // Request 4
        collector.RecordError(spiderId, new Exception("Network error"));
        // No response or item for error

        var stats = collector.GetStats(spiderId);

        // Assert
        stats.SpiderId.Should().Be(spiderId);
        stats.RequestCount.Should().Be(4);
        stats.ResponseCount.Should().Be(3);
        stats.ErrorCount.Should().Be(1);
        stats.ItemCount.Should().Be(2);
        stats.AverageResponseTime.Should().Be(100); // (100 + 150 + 50) / 3 = 100
        stats.StatusCodeDistribution[200].Should().Be(2);
        stats.StatusCodeDistribution[404].Should().Be(1);
        stats.TotalDuration.Should().BeGreaterThan(TimeSpan.Zero);
        stats.StartTime.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }
}
