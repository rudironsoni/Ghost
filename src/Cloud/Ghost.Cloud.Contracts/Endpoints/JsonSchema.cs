namespace Ghost.Cloud.Contracts.Endpoints;

[GenerateSerializer]
public sealed record JsonSchema
{
    [Id(0)] public string Schema { get; init; } = "http://json-schema.org/draft-07/schema#";
    [Id(1)] public string Type { get; init; } = "object";
    [Id(2)] public Dictionary<string, JsonElement> Properties { get; init; } = new();
    [Id(3)] public List<string> Required { get; init; } = new();
}
