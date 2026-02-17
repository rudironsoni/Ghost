using Ghost.Cloud.Contracts.Events;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Grains.State;

namespace Ghost.Cloud.Grains.Implementation;

public sealed class SchedulerGrain : Grain, ISchedulerGrain
{
    private readonly IPersistentState<SchedulerState> _state;

    public SchedulerGrain([PersistentState("scheduler", "Default")] IPersistentState<SchedulerState> state)
    {
        _state = state;
    }

    public Task ScheduleRunAsync(string runId, DateTimeOffset scheduledTime)
    {
        var scheduledRun = new ScheduledRun
        {
            RunId = runId,
            ScheduledTime = scheduledTime,
            Status = "Pending"
        };

        _state.State.ScheduledRuns[runId] = scheduledRun;
        return _state.WriteStateAsync();
    }

    public Task CancelScheduledRunAsync(string runId)
    {
        if (_state.State.ScheduledRuns.TryGetValue(runId, out ScheduledRun? run))
        {
            run.Status = "Cancelled";
            return _state.WriteStateAsync();
        }

        return Task.CompletedTask;
    }

    public Task<List<string>> GetPendingRunsAsync()
    {
        var pendingRuns = _state.State.ScheduledRuns
            .Where(r => r.Value.Status == "Pending" && r.Value.ScheduledTime > DateTimeOffset.UtcNow)
            .Select(r => r.Key)
            .ToList();

        return Task.FromResult(pendingRuns);
    }
}

[GenerateSerializer]
public sealed class SchedulerState
{
    [Id(0)] public Dictionary<string, ScheduledRun> ScheduledRuns { get; set; } = new();
}

[GenerateSerializer]
public sealed class ScheduledRun
{
    [Id(0)] public string RunId { get; set; } = string.Empty;
    [Id(1)] public DateTimeOffset ScheduledTime { get; set; }
    [Id(2)] public string Status { get; set; } = "Pending";
    [Id(3)] public string? EndpointId { get; set; }
    [Id(4)] public JsonElement Input { get; set; }
}
