using Ghost.Cloud.Contracts.Delivery;

namespace Ghost.Cloud.Contracts.Runs;

[GenerateSerializer]
public sealed record ScrapeRunRequest
{
    [Id(0)] public string EndpointId { get; init; } = string.Empty;
    [Id(1)] public JsonElement Input { get; init; }
    [Id(2)] public DeliveryConfig? Delivery { get; init; }
    [Id(3)] public string? IdempotencyKey { get; init; }
    [Id(4)] public string RequestedMode { get; init; } = "async";
    [Id(5)] public required Guid TenantId { get; init; }
}
