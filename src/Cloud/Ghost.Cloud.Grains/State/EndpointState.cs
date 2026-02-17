using Ghost.Cloud.Contracts.Endpoints;

namespace Ghost.Cloud.Grains.State;

[GenerateSerializer]
public sealed class EndpointState
{
    [Id(0)] public string EndpointId { get; set; } = string.Empty;
    [Id(1)] public string Version { get; set; } = "1.0.0";
    [Id(2)] public string PluginId { get; set; } = string.Empty;
    [Id(3)] public string DisplayName { get; set; } = string.Empty;
    [Id(4)] public EndpointCapability Capability { get; set; }
    [Id(5)] public JsonSchema InputSchema { get; set; } = new();
    [Id(6)] public JsonSchema OutputSchema { get; set; } = new();
    [Id(7)] public List<string> SupportedDeliveryModes { get; set; } = new();
    [Id(8)] public bool IsHealthy { get; set; } = true;
    [Id(9)] public string? LastErrorMessage { get; set; }
    [Id(10)] public DateTimeOffset LastHealthCheck { get; set; } = DateTimeOffset.UtcNow;
}
