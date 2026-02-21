namespace Ghost.Cloud.Contracts.Runs;

/// <summary>
/// Supported run kinds for scheduled execution.
/// </summary>
public enum RunKind
{
    /// <summary>Canary health check run.</summary>
    Canary,

    /// <summary>Cassette refresh run.</summary>
    CassetteRefresh,

    /// <summary>Replay run for deterministic testing.</summary>
    Replay
}

/// <summary>
/// Schedule types supported by the scheduler.
/// </summary>
public enum ScheduleType
{
    /// <summary>One-time scheduled run.</summary>
    OneTime,

    /// <summary>Recurring scheduled run with cron expression.</summary>
    Recurring
}

[GenerateSerializer]
public sealed record ScheduledRunRequest
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public DateTimeOffset ScheduledTime { get; init; }
    [Id(2)] public string EndpointId { get; init; } = string.Empty;
    [Id(3)] public required Guid TenantId { get; init; }
    [Id(4)] public JsonElement Input { get; init; }
    [Id(5)] public string RequestedMode { get; init; } = "canary";
    [Id(6)] public string RunKind { get; init; } = "canary";

    // CL-003: Recurring schedule support
    [Id(7)] public ScheduleType ScheduleType { get; init; } = ScheduleType.OneTime;
    [Id(8)] public RecurringSchedule? RecurringSchedule { get; init; }
}

/// <summary>
/// Configuration for a recurring schedule.
/// </summary>
[GenerateSerializer]
public sealed record RecurringSchedule
{
    /// <summary>
    /// Cron expression for the schedule (e.g., "0 0 * * *" for daily at midnight).
    /// </summary>
    [Id(0)] public string CronExpression { get; init; } = string.Empty;

    /// <summary>
    /// Time zone ID for the schedule (e.g., "UTC", "America/New_York").
    /// Defaults to UTC.
    /// </summary>
    [Id(1)] public string TimeZoneId { get; init; } = "UTC";

    /// <summary>
    /// Optional end time for the recurring schedule.
    /// </summary>
    [Id(2)] public DateTimeOffset? EndTime { get; init; }

    /// <summary>
    /// Maximum number of occurrences. Null for unlimited.
    /// </summary>
    [Id(3)] public int? MaxOccurrences { get; init; }
}

[GenerateSerializer]
public sealed record ScheduledRunInfo
{
    [Id(0)] public string RunId { get; init; } = string.Empty;
    [Id(1)] public DateTimeOffset ScheduledTime { get; init; }
    [Id(2)] public string Status { get; init; } = "Pending";
    [Id(3)] public string EndpointId { get; init; } = string.Empty;
    [Id(4)] public required Guid TenantId { get; init; }
    [Id(5)] public JsonElement Input { get; init; }
    [Id(6)] public string RequestedMode { get; init; } = "canary";
    [Id(7)] public string RunKind { get; init; } = "canary";
    [Id(8)] public string? Classification { get; init; }
    [Id(9)] public string? DiagnosticsUri { get; init; }
    [Id(10)] public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;

    // CL-003: Recurring schedule support
    [Id(11)] public ScheduleType ScheduleType { get; init; } = ScheduleType.OneTime;
    [Id(12)] public RecurringSchedule? RecurringSchedule { get; init; }
    [Id(13)] public int OccurrenceCount { get; init; }
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
