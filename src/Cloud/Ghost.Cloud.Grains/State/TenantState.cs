using Ghost.Cloud.Contracts.Runs;

namespace Ghost.Cloud.Grains.State;

[GenerateSerializer]
public sealed class TenantState
{
    [Id(0)] public Guid TenantId { get; set; }
    [Id(1)] public string Name { get; set; } = string.Empty;
    [Id(2)] public int MaxConcurrentRuns { get; set; } = 5;
    [Id(3)] public int DailyRunLimit { get; set; } = 1000;
    [Id(4)] public int CurrentRunCount { get; set; }
    [Id(5)] public DateTimeOffset LastResetDate { get; set; } = DateTimeOffset.UtcNow;
    [Id(6)] public List<string> ActiveRuns { get; set; } = new();
    [Id(7)] public List<RunAuthorizationAuditEntry> AuthorizationAudit { get; set; } = new();
}
