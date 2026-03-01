using FluentAssertions;
using Ghost.Sdk.Statistics;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Sdk.Tests.Statistics;

[Trait("Category", "Unit")]
public class SpiderStatsTests : ReliabilityTestBase
{
    public SpiderStatsTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Constructor_InitializesWithDefaultValues()
    {
        // Act
        var stats = new SpiderStats();

        // Assert
        stats.SpiderId.Should().Be(string.Empty);
        stats.RequestCount.Should().Be(0);
        stats.ResponseCount.Should().Be(0);
        stats.ErrorCount.Should().Be(0);
        stats.ItemCount.Should().Be(0);
        stats.StatusCodeDistribution.Should().NotBeNull();
        stats.StatusCodeDistribution.Should().BeEmpty();
        stats.TotalDuration.Should().Be(TimeSpan.Zero);
        stats.StartTime.Should().Be(default);
        stats.AverageResponseTime.Should().Be(0);
    }

    [Fact]
    public void RequestsPerSecond_WithZeroDuration_ReturnsZero()
    {
        // Arrange
        var stats = new SpiderStats
        {
            RequestCount = 100,
            TotalDuration = TimeSpan.Zero
        };

        // Act
        var rps = stats.RequestsPerSecond;

        // Assert
        rps.Should().Be(0);
    }

    [Fact]
    public void RequestsPerSecond_WithValidDuration_CalculatesCorrectly()
    {
        // Arrange
        var stats = new SpiderStats
        {
            RequestCount = 100,
            TotalDuration = TimeSpan.FromSeconds(10)
        };

        // Act
        var rps = stats.RequestsPerSecond;

        // Assert
        rps.Should().Be(10); // 100 requests / 10 seconds = 10 rps
    }

    [Fact]
    public void RequestsPerSecond_WithFractionalResult_ReturnsDecimal()
    {
        // Arrange
        var stats = new SpiderStats
        {
            RequestCount = 75,
            TotalDuration = TimeSpan.FromSeconds(30)
        };

        // Act
        var rps = stats.RequestsPerSecond;

        // Assert
        rps.Should().Be(2.5); // 75 requests / 30 seconds = 2.5 rps
    }

    [Fact]
    public void RequestsPerSecond_WithLessThanOneSecond_CalculatesCorrectly()
    {
        // Arrange
        var stats = new SpiderStats
        {
            RequestCount = 5,
            TotalDuration = TimeSpan.FromMilliseconds(500)
        };

        // Act
        var rps = stats.RequestsPerSecond;

        // Assert
        rps.Should().Be(10); // 5 requests / 0.5 seconds = 10 rps
    }

    [Fact]
    public void StatusCodeDistribution_CanBeModified()
    {
        // Arrange
        var stats = new SpiderStats();

        // Act
        stats.StatusCodeDistribution.TryAdd(200, 10);
        stats.StatusCodeDistribution.TryAdd(404, 2);
        stats.StatusCodeDistribution.TryAdd(500, 1);

        // Assert
        stats.StatusCodeDistribution.Should().HaveCount(3);
        stats.StatusCodeDistribution[200].Should().Be(10);
        stats.StatusCodeDistribution[404].Should().Be(2);
        stats.StatusCodeDistribution[500].Should().Be(1);
    }

    [Fact]
    public void Properties_CanBeSetAndRetrieved()
    {
        // Arrange
        var spiderId = "test-spider";
        var startTime = DateTimeOffset.UtcNow;
        var duration = TimeSpan.FromMinutes(5);

        // Act
        var stats = new SpiderStats
        {
            SpiderId = spiderId,
            RequestCount = 100,
            ResponseCount = 95,
            ErrorCount = 5,
            ItemCount = 90,
            TotalDuration = duration,
            StartTime = startTime,
            AverageResponseTime = 250.5
        };

        // Assert
        stats.SpiderId.Should().Be(spiderId);
        stats.RequestCount.Should().Be(100);
        stats.ResponseCount.Should().Be(95);
        stats.ErrorCount.Should().Be(5);
        stats.ItemCount.Should().Be(90);
        stats.TotalDuration.Should().Be(duration);
        stats.StartTime.Should().Be(startTime);
        stats.AverageResponseTime.Should().Be(250.5);
    }

    [Fact]
    public void SpiderStats_SupportsLargeCountValues()
    {
        // Arrange & Act
        var stats = new SpiderStats
        {
            RequestCount = long.MaxValue,
            ResponseCount = long.MaxValue - 1,
            ErrorCount = long.MaxValue - 2,
            ItemCount = long.MaxValue - 3
        };

        // Assert
        stats.RequestCount.Should().Be(long.MaxValue);
        stats.ResponseCount.Should().Be(long.MaxValue - 1);
        stats.ErrorCount.Should().Be(long.MaxValue - 2);
        stats.ItemCount.Should().Be(long.MaxValue - 3);
    }

    [Fact]
    public void RequestsPerSecond_WithHighVolume_CalculatesCorrectly()
    {
        // Arrange
        var stats = new SpiderStats
        {
            RequestCount = 1_000_000,
            TotalDuration = TimeSpan.FromHours(1)
        };

        // Act
        var rps = stats.RequestsPerSecond;

        // Assert
        rps.Should().BeApproximately(277.78, 0.01); // 1M requests / 3600 seconds ≈ 277.78 rps
    }

    [Fact]
    public void StatusCodeDistribution_SupportsMultipleStatusCodes()
    {
        // Arrange
        var stats = new SpiderStats();

        // Act - Add various HTTP status codes
        stats.StatusCodeDistribution.TryAdd(200, 1000); // OK
        stats.StatusCodeDistribution.TryAdd(201, 50);   // Created
        stats.StatusCodeDistribution.TryAdd(204, 25);   // No Content
        stats.StatusCodeDistribution.TryAdd(301, 10);   // Moved Permanently
        stats.StatusCodeDistribution.TryAdd(302, 5);    // Found
        stats.StatusCodeDistribution.TryAdd(400, 20);   // Bad Request
        stats.StatusCodeDistribution.TryAdd(401, 3);    // Unauthorized
        stats.StatusCodeDistribution.TryAdd(403, 2);    // Forbidden
        stats.StatusCodeDistribution.TryAdd(404, 100);  // Not Found
        stats.StatusCodeDistribution.TryAdd(429, 15);   // Too Many Requests
        stats.StatusCodeDistribution.TryAdd(500, 8);    // Internal Server Error
        stats.StatusCodeDistribution.TryAdd(502, 4);    // Bad Gateway
        stats.StatusCodeDistribution.TryAdd(503, 2);    // Service Unavailable

        // Assert
        stats.StatusCodeDistribution.Should().HaveCount(13);
        stats.StatusCodeDistribution.Values.Sum().Should().Be(1244);
    }
}
