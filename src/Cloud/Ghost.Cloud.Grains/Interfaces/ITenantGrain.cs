using Ghost.Cloud.Contracts.Runs;

namespace Ghost.Cloud.Grains.Interfaces;

public interface ITenantGrain : IGrainWithGuidKey
{
    public Task<RunAuthorizationDecision> AuthorizeRunAsync(string runId, string endpointId);
    public Task RecordUsageAsync(string runId, int itemsScraped, long executionTimeMs);
    public Task<bool> IsRunAllowedAsync(string endpointId);
    public Task<IReadOnlyList<RunAuthorizationAuditEntry>> GetAuthorizationAuditAsync(int maxEntries);
}
