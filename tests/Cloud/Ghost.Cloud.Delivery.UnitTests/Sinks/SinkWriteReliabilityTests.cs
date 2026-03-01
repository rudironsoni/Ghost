using System.Text;
using Ghost.Cloud.Delivery.Sinks;
using Ghost.Testing.Reliability;
using Xunit.Abstractions;

namespace Ghost.Cloud.Delivery.UnitTests.Sinks;

public sealed class SinkWriteReliabilityTests : ReliabilityTestBase
{
    public SinkWriteReliabilityTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void Create_WithCursor_ProducesStableIdempotencyKeyAndObjectName()
    {
        byte[] payload = Encoding.UTF8.GetBytes("""{"id":"1","title":"engineer"}""");

        SinkWritePlan firstPlan = SinkWritePlanner.Create("deliveries/jobs", "json", "cursor:page/1", payload);
        SinkWritePlan secondPlan = SinkWritePlanner.Create("deliveries/jobs", "json", "cursor:page/1", payload);

        firstPlan.IdempotencyKey.Should().Be("cursor_cursor_page_1");
        firstPlan.ObjectName.Should().Be("deliveries/jobs/cursor_cursor_page_1.json");
        firstPlan.IntegritySha256.Should().Be(secondPlan.IntegritySha256);
    }

    [Fact]
    public void Create_WithoutCursor_UsesPayloadHashIdempotency()
    {
        byte[] firstPayload = Encoding.UTF8.GetBytes("""{"id":"1"}""");
        byte[] secondPayload = Encoding.UTF8.GetBytes("""{"id":"2"}""");

        SinkWritePlan firstPlan = SinkWritePlanner.Create("deliveries", "ndjson", cursor: null, firstPayload);
        SinkWritePlan secondPlan = SinkWritePlanner.Create("deliveries", "ndjson", cursor: null, secondPayload);

        firstPlan.IdempotencyKey.Should().StartWith("payload_");
        secondPlan.IdempotencyKey.Should().StartWith("payload_");
        firstPlan.IdempotencyKey.Should().NotBe(secondPlan.IdempotencyKey);
        firstPlan.ObjectName.Should().EndWith(".ndjson");
    }

    [Fact]
    public void Tracker_AllowsRetryAfterFailure()
    {
        byte[] payload = Encoding.UTF8.GetBytes("""{"id":"1"}""");
        SinkWritePlan plan = SinkWritePlanner.Create("deliveries", "json", "cursor_1", payload);
        var tracker = new SinkWriteTracker();

        bool firstTry = tracker.TryStart(plan);
        bool duplicateTry = tracker.TryStart(plan);
        tracker.MarkFailed(plan);
        bool retryAfterFailure = tracker.TryStart(plan);

        firstTry.Should().BeTrue();
        duplicateTry.Should().BeFalse();
        retryAfterFailure.Should().BeTrue();
    }
}
