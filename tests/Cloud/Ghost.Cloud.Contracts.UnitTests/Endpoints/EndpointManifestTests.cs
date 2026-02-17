using Ghost.Cloud.Contracts.Endpoints;

namespace Ghost.Cloud.Contracts.UnitTests.Endpoints;

public class EndpointManifestTests
{
    [Fact]
    public void EndpointManifest_DefaultValues_AreSet()
    {
        var manifest = new EndpointManifest();

        manifest.EndpointId.Should().BeEmpty();
        manifest.Version.Should().Be("1.0.0");
        manifest.PluginId.Should().BeEmpty();
        manifest.SupportedDeliveryModes.Should().Contain("sync").And.Contain("async");
        manifest.Limits.MaxSyncTimeoutSeconds.Should().Be(30);
    }

    [Fact]
    public void EndpointManifest_WithValues_CanBeCreated()
    {
        var manifest = new EndpointManifest
        {
            EndpointId = "test-endpoint-v1",
            Version = "2.0.0",
            PluginId = "ghost.plugin.test",
            DisplayName = "Test Endpoint",
            Capability = EndpointCapability.Search
        };

        manifest.EndpointId.Should().Be("test-endpoint-v1");
        manifest.Version.Should().Be("2.0.0");
        manifest.Capability.Should().Be(EndpointCapability.Search);
    }
}
