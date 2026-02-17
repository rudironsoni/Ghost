namespace Ghost.Cloud.Grains.Interfaces;

public interface ISchedulerGrain : IGrainWithStringKey
{
    public Task ScheduleRunAsync(string runId, DateTimeOffset scheduledTime);
    public Task CancelScheduledRunAsync(string runId);
    public Task<List<string>> GetPendingRunsAsync();
}
