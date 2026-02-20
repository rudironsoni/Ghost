namespace Ghost.Cloud.Contracts.Runs;

[GenerateSerializer]
public sealed record ScrapeRunStatus
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public string EndpointId { get; init; } = string.Empty;
    [Id(2)] public string Status { get; init; } = "Pending";
    [Id(3)] public int ItemsDiscovered { get; init; }
    [Id(4)] public int ItemsDelivered { get; init; }
    [Id(5)] public DateTimeOffset StartedAt { get; init; }
    [Id(6)] public DateTimeOffset? CompletedAt { get; init; }
    [Id(7)] public string? ErrorMessage { get; init; }
    [Id(8)] public string? ResultLocation { get; init; }
    [Id(9)] public DeliveryProgress DeliveryProgress { get; init; } = new();
    [Id(10)] public string? ErrorCode { get; init; }
}
