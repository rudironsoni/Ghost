namespace Ghost.Cloud.Contracts.Runs;

[GenerateSerializer]
public sealed record RunAuthorizationDecision
{
    [Id(0)] public bool IsAuthorized { get; init; }
    [Id(1)] public string Code { get; init; } = string.Empty;
    [Id(2)] public string Message { get; init; } = string.Empty;
    [Id(3)] public DateTimeOffset EvaluatedAt { get; init; } = DateTimeOffset.UtcNow;
    [Id(4)] public int CurrentRunCount { get; init; }
    [Id(5)] public int ActiveRunCount { get; init; }
    [Id(6)] public int DailyRunLimit { get; init; }
    [Id(7)] public int MaxConcurrentRuns { get; init; }
}

[GenerateSerializer]
public sealed record RunAuthorizationAuditEntry
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public string EndpointId { get; init; } = string.Empty;
    [Id(2)] public RunAuthorizationDecision Decision { get; init; } = new();
}
