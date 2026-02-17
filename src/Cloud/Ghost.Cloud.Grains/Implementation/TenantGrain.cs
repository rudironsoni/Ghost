using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Grains.State;
using Orleans.Runtime;

namespace Ghost.Cloud.Grains.Implementation;

public sealed class TenantGrain : Grain, ITenantGrain
{
    private readonly IPersistentState<TenantState> _state;

    public TenantGrain([PersistentState("tenant", "Default")] IPersistentState<TenantState> state)
    {
        _state = state;
    }

    public Task<bool> AuthorizeRunAsync(string runId, string endpointId)
    {
        CheckAndResetDailyLimit();

        if (_state.State.CurrentRunCount >= _state.State.DailyRunLimit)
        {
            return Task.FromResult(false);
        }

        if (_state.State.ActiveRuns.Count >= _state.State.MaxConcurrentRuns)
        {
            return Task.FromResult(false);
        }

        _state.State.ActiveRuns.Add(runId);
        _state.State.CurrentRunCount++;
        return Task.FromResult(true);
    }

    public Task RecordUsageAsync(string runId, int itemsScraped, long executionTimeMs)
    {
        _state.State.ActiveRuns.Remove(runId);
        return Task.CompletedTask;
    }

    public Task<bool> IsRunAllowedAsync(string endpointId)
    {
        CheckAndResetDailyLimit();
        return Task.FromResult(_state.State.CurrentRunCount < _state.State.DailyRunLimit);
    }

    private void CheckAndResetDailyLimit()
    {
        if (_state.State.LastResetDate.Date != DateTimeOffset.UtcNow.Date)
        {
            _state.State.CurrentRunCount = 0;
            _state.State.LastResetDate = DateTimeOffset.UtcNow;
        }
    }
}
