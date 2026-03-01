using Ghost.Cloud.Contracts.Runs;

namespace Ghost.Cloud.Grains.Interfaces;

public interface ISchedulerGrain : IGrainWithStringKey
{
    public Task ScheduleRunAsync(ScheduledRunRequest request);
    public Task CancelScheduledRunAsync(string runId);
    public Task<List<ScheduledRunInfo>> GetPendingRunsAsync();
    public Task<List<ScheduledRunInfo>> GetDueRunsAsync(DateTimeOffset asOfUtc, int maxCount);
    public Task MarkRunStatusAsync(string runId, string status, string? classification, string? diagnosticsUri);
}
