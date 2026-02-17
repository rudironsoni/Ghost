namespace Ghost.Cloud.Contracts.Runs;

[GenerateSerializer]
public sealed record ScrapeRunResult<T>
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public List<T> Items { get; init; } = new();
    [Id(2)] public string? NextCursor { get; init; }
    [Id(3)] public bool HasMore { get; init; }
}
