namespace Ghost.Cloud.Contracts.Runs;

[GenerateSerializer]
public sealed record DeliveryProgress
{
    [Id(0)] public int BatchesTotal { get; init; }
    [Id(1)] public int BatchesDelivered { get; init; }
    [Id(2)] public long BytesDelivered { get; init; }
    [Id(3)] public string? LastCursor { get; init; }
}
