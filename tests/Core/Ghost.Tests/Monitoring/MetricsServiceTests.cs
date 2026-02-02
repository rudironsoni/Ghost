using FluentAssertions;
using Ghost.Monitoring;
using Xunit;

namespace Ghost.Tests.Monitoring;

public class MetricsServiceTests
{
    [Fact]
    public void RecordRequest_IncrementsTotalRequests()
    {
        var service = new MetricsService();

        service.RecordRequest();
        service.RecordRequest();

        var snapshot = service.GetSnapshot();

        snapshot.TotalRequests.Should().Be(2);
    }

    [Fact]
    public void GetSnapshot_DoesNotResetCounters()
    {
        var service = new MetricsService();

        service.RecordRequest();
        var first = service.GetSnapshot();

        service.RecordRequest();
        var second = service.GetSnapshot();

        first.TotalRequests.Should().Be(1);
        second.TotalRequests.Should().Be(2);
    }

    [Fact]
    public void GetSnapshot_SetsTimestampWithinCallWindow()
    {
        var service = new MetricsService();
        var before = DateTimeOffset.UtcNow;

        var snapshot = service.GetSnapshot();

        var after = DateTimeOffset.UtcNow;
        snapshot.Timestamp.Should().BeOnOrAfter(before);
        snapshot.Timestamp.Should().BeOnOrBefore(after);
    }
}
