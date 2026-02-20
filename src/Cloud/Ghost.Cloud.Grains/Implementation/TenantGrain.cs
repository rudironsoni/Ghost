using System.Diagnostics;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Grains.Observability;
using Ghost.Cloud.Grains.State;
using Orleans.Runtime;

namespace Ghost.Cloud.Grains.Implementation;

public sealed class TenantGrain : Grain, ITenantGrain
{
    private const int MaxAuditEntries = 200;
    private readonly IPersistentState<TenantState> _state;

    public TenantGrain([PersistentState("tenant", "Default")] IPersistentState<TenantState> state)
    {
        _state = state;
    }

    public async Task<RunAuthorizationDecision> AuthorizeRunAsync(string runId, string endpointId)
    {
        using Activity? activity = CloudGrainsTelemetry.ActivitySource.StartActivity("CloudGrains.Tenant.AuthorizeRun");
        activity?.SetTag("ghost.run.id", runId);
        activity?.SetTag("ghost.endpoint.id", endpointId);

        bool stateChanged = CheckAndResetDailyLimit();

        if (_state.State.ActiveRuns.Contains(runId, StringComparer.Ordinal))
        {
            RunAuthorizationDecision idempotentDecision = BuildDecision(
                isAuthorized: true,
                code: "ALREADY_AUTHORIZED",
                message: "Run was already authorized and is currently active.");
            CloudGrainsTelemetry.RecordAuthorizationDecision(idempotentDecision.Code, idempotentDecision.IsAuthorized);
            activity?.SetTag("ghost.authorization.code", idempotentDecision.Code);
            activity?.SetStatus(ActivityStatusCode.Ok);
            AppendAudit(runId, endpointId, idempotentDecision);
            await _state.WriteStateAsync().ConfigureAwait(false);
            return idempotentDecision;
        }

        if (_state.State.CurrentRunCount >= _state.State.DailyRunLimit)
        {
            RunAuthorizationDecision quotaDecision = BuildDecision(
                isAuthorized: false,
                code: "DAILY_QUOTA_EXCEEDED",
                message: "Tenant daily run limit has been reached.");
            CloudGrainsTelemetry.RecordAuthorizationDecision(quotaDecision.Code, quotaDecision.IsAuthorized);
            activity?.SetTag("ghost.authorization.code", quotaDecision.Code);
            activity?.SetStatus(ActivityStatusCode.Error, quotaDecision.Message);
            AppendAudit(runId, endpointId, quotaDecision);
            await _state.WriteStateAsync().ConfigureAwait(false);
            return quotaDecision;
        }

        if (_state.State.ActiveRuns.Count >= _state.State.MaxConcurrentRuns)
        {
            RunAuthorizationDecision concurrentDecision = BuildDecision(
                isAuthorized: false,
                code: "MAX_CONCURRENT_RUNS_EXCEEDED",
                message: "Tenant max concurrent run limit has been reached.");
            CloudGrainsTelemetry.RecordAuthorizationDecision(concurrentDecision.Code, concurrentDecision.IsAuthorized);
            activity?.SetTag("ghost.authorization.code", concurrentDecision.Code);
            activity?.SetStatus(ActivityStatusCode.Error, concurrentDecision.Message);
            AppendAudit(runId, endpointId, concurrentDecision);
            await _state.WriteStateAsync().ConfigureAwait(false);
            return concurrentDecision;
        }

        _state.State.ActiveRuns.Add(runId);
        _state.State.CurrentRunCount++;
        RunAuthorizationDecision authorizedDecision = BuildDecision(
            isAuthorized: true,
            code: "AUTHORIZED",
            message: "Run authorized for execution.");
        CloudGrainsTelemetry.RecordAuthorizationDecision(authorizedDecision.Code, authorizedDecision.IsAuthorized);
        activity?.SetTag("ghost.authorization.code", authorizedDecision.Code);
        activity?.SetStatus(ActivityStatusCode.Ok);
        AppendAudit(runId, endpointId, authorizedDecision);
        stateChanged = true;

        if (stateChanged)
        {
            await _state.WriteStateAsync().ConfigureAwait(false);
        }

        return authorizedDecision;
    }

    public async Task RecordUsageAsync(string runId, int itemsScraped, long executionTimeMs)
    {
        _state.State.ActiveRuns.Remove(runId);
        await _state.WriteStateAsync().ConfigureAwait(false);
    }

    public async Task<bool> IsRunAllowedAsync(string endpointId)
    {
        bool stateChanged = CheckAndResetDailyLimit();
        if (stateChanged)
        {
            await _state.WriteStateAsync().ConfigureAwait(false);
        }

        return _state.State.CurrentRunCount < _state.State.DailyRunLimit;
    }

    public Task<IReadOnlyList<RunAuthorizationAuditEntry>> GetAuthorizationAuditAsync(int maxEntries)
    {
        int safeMaxEntries = maxEntries <= 0 ? 50 : maxEntries;

        IReadOnlyList<RunAuthorizationAuditEntry> entries = _state.State.AuthorizationAudit
            .TakeLast(safeMaxEntries)
            .ToList();

        return Task.FromResult(entries);
    }

    private bool CheckAndResetDailyLimit()
    {
        if (_state.State.LastResetDate.Date != DateTimeOffset.UtcNow.Date)
        {
            _state.State.CurrentRunCount = 0;
            _state.State.LastResetDate = DateTimeOffset.UtcNow;
            return true;
        }

        return false;
    }

    private RunAuthorizationDecision BuildDecision(bool isAuthorized, string code, string message) => new()
    {
        IsAuthorized = isAuthorized,
        Code = code,
        Message = message,
        CurrentRunCount = _state.State.CurrentRunCount,
        ActiveRunCount = _state.State.ActiveRuns.Count,
        DailyRunLimit = _state.State.DailyRunLimit,
        MaxConcurrentRuns = _state.State.MaxConcurrentRuns
    };

    private void AppendAudit(string runId, string endpointId, RunAuthorizationDecision decision)
    {
        _state.State.AuthorizationAudit.Add(new RunAuthorizationAuditEntry
        {
            RunId = runId,
            EndpointId = endpointId,
            Decision = decision
        });

        if (_state.State.AuthorizationAudit.Count > MaxAuditEntries)
        {
            _state.State.AuthorizationAudit.RemoveRange(
                0,
                _state.State.AuthorizationAudit.Count - MaxAuditEntries);
        }
    }
}
