namespace Ghost.Cloud.Contracts.Endpoints;

[GenerateSerializer]
public sealed record EndpointManifest
{
    [Id(0)] public string EndpointId { get; init; } = string.Empty;
    [Id(1)] public string Version { get; init; } = "1.0.0";
    [Id(2)] public string PluginId { get; init; } = string.Empty;
    [Id(3)] public string DisplayName { get; init; } = string.Empty;
    [Id(4)] public string Description { get; init; } = string.Empty;
    [Id(5)] public EndpointCapability Capability { get; init; }
    [Id(6)] public JsonSchema InputSchema { get; init; } = new();
    [Id(7)] public JsonSchema OutputSchema { get; init; } = new();
    [Id(8)] public List<string> SupportedDeliveryModes { get; init; } = ["sync", "async"];
    [Id(9)] public DeliveryLimits Limits { get; init; } = new();
    [Id(10)] public bool SupportsArtifacts { get; init; }
}
