namespace Ghost.Cloud.Contracts.Delivery;

[GenerateSerializer]
public sealed record DeliveryConfig
{
    [Id(0)] public ResultSink? ResultSink { get; init; }
    [Id(1)] public ArtifactSink? ArtifactSink { get; init; }
    [Id(2)] public string Format { get; init; } = "ndjson";
    [Id(3)] public List<string>? OutputFields { get; init; }
    [Id(4)] public int? BatchSize { get; init; }
}
