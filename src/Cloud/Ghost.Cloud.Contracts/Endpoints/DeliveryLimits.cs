namespace Ghost.Cloud.Contracts.Endpoints;

[GenerateSerializer]
public sealed record DeliveryLimits
{
    [Id(0)] public int MaxSyncTimeoutSeconds { get; init; } = 30;
    [Id(1)] public int MaxResultsPerSync { get; init; } = 100;
    [Id(2)] public int MaxResultsPerAsync { get; init; } = 10000;
    [Id(3)] public int MaxInputSizeBytes { get; init; } = 65536;
}
