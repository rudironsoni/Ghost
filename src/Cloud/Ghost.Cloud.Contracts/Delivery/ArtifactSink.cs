namespace Ghost.Cloud.Contracts.Delivery;

[GenerateSerializer]
public sealed record ArtifactSink
{
    [Id(0)] public string Type { get; init; } = string.Empty;
    [Id(1)] public string Prefix { get; init; } = string.Empty;
    [Id(2)] public StorageCredentials Credentials { get; init; } = new();
    [Id(3)] public List<string> Include { get; init; } = new();
}
