using FluentAssertions;
using Ghost.Monitoring;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Tests.Monitoring;

public class MetricsServiceTests : ReliabilityTestBase
{
    public MetricsServiceTests(ITestOutputHelper output) : base(output) { }

    [Fact]
    public void RecordRequestIncrementsTotalRequests()
    {
        var service = new MetricsService();

        service.RecordRequest();
        service.RecordRequest();

        MetricsSnapshot snapshot = service.GetSnapshot();

        snapshot.TotalRequests.Should().Be(2);
    }

    [Fact]
    public void GetSnapshotDoesNotResetCounters()
    {
        var service = new MetricsService();

        service.RecordRequest();
        MetricsSnapshot first = service.GetSnapshot();

        service.RecordRequest();
        MetricsSnapshot second = service.GetSnapshot();

        first.TotalRequests.Should().Be(1);
        second.TotalRequests.Should().Be(2);
    }

    [Fact]
    public void GetSnapshotSetsTimestampWithinCallWindow()
    {
        var service = new MetricsService();
        DateTimeOffset before = DateTimeOffset.UtcNow;

        MetricsSnapshot snapshot = service.GetSnapshot();

        DateTimeOffset after = DateTimeOffset.UtcNow;
        snapshot.Timestamp.Should().BeOnOrAfter(before);
        snapshot.Timestamp.Should().BeOnOrBefore(after);
    }
}
