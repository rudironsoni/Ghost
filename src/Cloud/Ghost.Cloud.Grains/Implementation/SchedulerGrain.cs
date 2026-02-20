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

    public Task ScheduleRunAsync(ScheduledRunRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var scheduledRun = new ScheduledRun
        {
            RunId = request.RunId,
            ScheduledTime = request.ScheduledTime,
            Status = "Pending",
            EndpointId = request.EndpointId,
            TenantId = request.TenantId,
            Input = request.Input,
            RequestedMode = request.RequestedMode,
            RunKind = request.RunKind,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _state.State.ScheduledRuns[request.RunId] = scheduledRun;
        return _state.WriteStateAsync();
    }

    public Task CancelScheduledRunAsync(string runId)
    {
        if (_state.State.ScheduledRuns.TryGetValue(runId, out ScheduledRun? run))
        {
            run.Status = "Cancelled";
            run.UpdatedAt = DateTimeOffset.UtcNow;
            return _state.WriteStateAsync();
        }

        return Task.CompletedTask;
    }

    public Task<List<ScheduledRunInfo>> GetPendingRunsAsync()
    {
        var pendingRuns = _state.State.ScheduledRuns
            .Where(run => run.Value.Status == "Pending")
            .Select(run => run.Value.ToInfo())
            .ToList();

        return Task.FromResult(pendingRuns);
    }

    public async Task<List<ScheduledRunInfo>> GetDueRunsAsync(DateTimeOffset asOfUtc, int maxCount)
    {
        List<ScheduledRun> dueRuns = _state.State.ScheduledRuns
            .Values
            .Where(run => run.Status == "Pending" && run.ScheduledTime <= asOfUtc)
            .OrderBy(run => run.ScheduledTime)
            .Take(maxCount)
            .ToList();

        foreach (ScheduledRun run in dueRuns)
        {
            run.Status = "Dispatching";
            run.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (dueRuns.Count > 0)
        {
            await _state.WriteStateAsync().ConfigureAwait(false);
        }

        return dueRuns.Select(run => run.ToInfo()).ToList();
    }

    public Task MarkRunStatusAsync(string runId, string status, string? classification, string? diagnosticsUri)
    {
        if (!_state.State.ScheduledRuns.TryGetValue(runId, out ScheduledRun? run))
        {
            return Task.CompletedTask;
        }

        run.Status = status;
        run.Classification = classification;
        run.DiagnosticsUri = diagnosticsUri;
        run.UpdatedAt = DateTimeOffset.UtcNow;

        return _state.WriteStateAsync();
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
    [Id(3)] public string EndpointId { get; set; } = string.Empty;
    [Id(4)] public JsonElement Input { get; set; }
    [Id(5)] public Guid TenantId { get; set; }
    [Id(6)] public string RequestedMode { get; set; } = "canary";
    [Id(7)] public string RunKind { get; set; } = "canary";
    [Id(8)] public string? Classification { get; set; }
    [Id(9)] public string? DiagnosticsUri { get; set; }
    [Id(10)] public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ScheduledRunInfo ToInfo() => new()
    {
        RunId = RunId,
        ScheduledTime = ScheduledTime,
        Status = Status,
        EndpointId = EndpointId,
        TenantId = TenantId,
        Input = Input,
        RequestedMode = RequestedMode,
        RunKind = RunKind,
        Classification = Classification,
        DiagnosticsUri = DiagnosticsUri,
        UpdatedAt = UpdatedAt
    };
}
