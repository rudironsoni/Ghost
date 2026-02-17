using Ghost.Cloud.Contracts.Delivery;
using Ghost.Cloud.Contracts.Runs;

namespace Ghost.Cloud.Contracts.UnitTests.Runs;

public class ScrapeRunRequestTests
{
    [Fact]
    public void ScrapeRunRequest_DefaultValues_AreSet()
    {
        var request = new ScrapeRunRequest();

        request.EndpointId.Should().BeEmpty();
        request.RequestedMode.Should().Be("async");
    }

    [Fact]
    public void ScrapeRunRequest_WithDeliveryConfig_CanBeCreated()
    {
        var request = new ScrapeRunRequest
        {
            EndpointId = "test-endpoint",
            Input = JsonDocument.Parse("{}").RootElement,
            RequestedMode = "sync",
            Delivery = new DeliveryConfig
            {
                Format = "json",
                ResultSink = new ResultSink { Type = "s3", Uri = "bucket/results" }
            }
        };

        request.EndpointId.Should().Be("test-endpoint");
        request.RequestedMode.Should().Be("sync");
        request.Delivery.Should().NotBeNull();
        request.Delivery!.Format.Should().Be("json");
    }
}
