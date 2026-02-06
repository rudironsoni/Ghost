using FluentAssertions;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInSessionPoolMetricsTests
{
    [Fact]
    public void MetricsPropertiesSetAndGet()
    {
        var metrics = new SessionPoolMetrics
        {
            AvailableCount = 2,
            InUseCount = 3,
            TotalCreated = 5,
            TotalRecycled = 4,
            TotalDisposed = 1,
            AverageAcquisitionTime = System.TimeSpan.FromMilliseconds(120),
            LastHealthCheck = new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)
        };

        metrics.AvailableCount.Should().Be(2);
        metrics.InUseCount.Should().Be(3);
        metrics.TotalCreated.Should().Be(5);
        metrics.TotalRecycled.Should().Be(4);
        metrics.TotalDisposed.Should().Be(1);
        metrics.AverageAcquisitionTime.Should().Be(System.TimeSpan.FromMilliseconds(120));
        metrics.LastHealthCheck.Should().Be(new System.DateTime(2026, 1, 1, 0, 0, 0, System.DateTimeKind.Utc));
    }
}
