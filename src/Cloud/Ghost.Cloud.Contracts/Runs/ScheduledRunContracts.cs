namespace Ghost.Cloud.Contracts.Runs;

[GenerateSerializer]
public sealed record ScheduledRunRequest
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public DateTimeOffset ScheduledTime { get; init; }
    [Id(2)] public string EndpointId { get; init; } = string.Empty;
    [Id(3)] public Guid TenantId { get; init; }
    [Id(4)] public JsonElement Input { get; init; }
    [Id(5)] public string RequestedMode { get; init; } = "canary";
    [Id(6)] public string RunKind { get; init; } = "canary";
}

[GenerateSerializer]
public sealed record ScheduledRunInfo
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public DateTimeOffset ScheduledTime { get; init; }
    [Id(2)] public string Status { get; init; } = "Pending";
    [Id(3)] public string EndpointId { get; init; } = string.Empty;
    [Id(4)] public Guid TenantId { get; init; }
    [Id(5)] public JsonElement Input { get; init; }
    [Id(6)] public string RequestedMode { get; init; } = "canary";
    [Id(7)] public string RunKind { get; init; } = "canary";
    [Id(8)] public string? Classification { get; init; }
    [Id(9)] public string? DiagnosticsUri { get; init; }
    [Id(10)] public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

[GenerateSerializer]
public sealed record CanaryRunOutcome
{
    [Id(0)] public bool Success { get; init; }
    [Id(1)] public string Classification { get; init; } = "Unknown";
    [Id(2)] public string? DiagnosticsUri { get; init; }
    [Id(3)] public int ItemsDiscovered { get; init; }
    [Id(4)] public int ArtifactsCaptured { get; init; }
    [Id(5)] public string? ErrorMessage { get; init; }
}
