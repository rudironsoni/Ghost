namespace Ghost.Cloud.Contracts.Delivery;

[GenerateSerializer]
public sealed record ResultSink
{
    [Id(0)] public string Type { get; init; } = string.Empty;
    [Id(1)] public string? Uri { get; init; }
    [Id(2)] public StorageCredentials? Credentials { get; init; }
}
