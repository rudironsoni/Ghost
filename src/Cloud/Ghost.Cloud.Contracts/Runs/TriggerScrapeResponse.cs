namespace Ghost.Cloud.Contracts.Runs;

[GenerateSerializer]
public sealed record TriggerScrapeResponse
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public string Status { get; init; } = "Pending";
    [Id(2)] public string? ResultSinkUri { get; init; }
    [Id(3)] public DateTimeOffset? EstimatedCompletion { get; init; }
}
