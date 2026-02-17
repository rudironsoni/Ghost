using Ghost.Cloud.Contracts.Delivery;

namespace Ghost.Cloud.Contracts.Runs;

[GenerateSerializer]
public sealed record TriggerScrapeRequest
{
    [Id(0)] public JsonElement Input { get; init; }
    [Id(1)] public DeliveryConfig? Delivery { get; init; }
    [Id(2)] public string? IdempotencyKey { get; init; }
}
