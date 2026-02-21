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
        activity?.SetTag("ghost.tenant.id", _state.State.TenantId);

        // CL-002: Deterministic quota check with validation
        CheckAndResetDailyLimit();

        // Validate inputs first for deterministic failure
        if (string.IsNullOrWhiteSpace(runId))
        {
            RunAuthorizationDecision validationDecision = BuildDecision(
                isAuthorized: false,
                code: "VALIDATION_FAILED",
                message: "Run ID cannot be null or empty.",
                classification: AuthorizationDecisionClassification.ValidationFailed);
            CloudGrainsTelemetry.RecordAuthorizationDecision(validationDecision.Code, validationDecision.IsAuthorized);
            activity?.SetTag("ghost.authorization.code", validationDecision.Code);
            activity?.SetStatus(ActivityStatusCode.Error, validationDecision.Message);
            await AppendAuditAsync(runId ?? "unknown", endpointId, validationDecision).ConfigureAwait(false);
            return validationDecision;
        }

        if (string.IsNullOrWhiteSpace(endpointId))
        {
            RunAuthorizationDecision validationDecision = BuildDecision(
                isAuthorized: false,
                code: "VALIDATION_FAILED",
                message: "Endpoint ID cannot be null or empty.",
                classification: AuthorizationDecisionClassification.ValidationFailed);
            CloudGrainsTelemetry.RecordAuthorizationDecision(validationDecision.Code, validationDecision.IsAuthorized);
            activity?.SetTag("ghost.authorization.code", validationDecision.Code);
            activity?.SetStatus(ActivityStatusCode.Error, validationDecision.Message);
            await AppendAuditAsync(runId, endpointId ?? "unknown", validationDecision).ConfigureAwait(false);
            return validationDecision;
        }

        // Idempotency check - deterministic outcome for duplicate requests
        if (_state.State.ActiveRuns.Contains(runId, StringComparer.Ordinal))
        {
            RunAuthorizationDecision idempotentDecision = BuildDecision(
                isAuthorized: true,
                code: "ALREADY_AUTHORIZED",
                message: "Run was already authorized and is currently active.",
                classification: AuthorizationDecisionClassification.Idempotent);
            CloudGrainsTelemetry.RecordAuthorizationDecision(idempotentDecision.Code, idempotentDecision.IsAuthorized);
            activity?.SetTag("ghost.authorization.code", idempotentDecision.Code);
            activity?.SetStatus(ActivityStatusCode.Ok);
            await AppendAuditAsync(runId, endpointId, idempotentDecision).ConfigureAwait(false);
            await _state.WriteStateAsync().ConfigureAwait(false);
            return idempotentDecision;
        }

        // CL-002: Deterministic quota check with clear classification
        if (_state.State.CurrentRunCount >= _state.State.DailyRunLimit)
        {
            RunAuthorizationDecision quotaDecision = BuildDecision(
                isAuthorized: false,
                code: "DAILY_QUOTA_EXCEEDED",
                message: $"Tenant daily run limit ({_state.State.DailyRunLimit}) has been reached. Current count: {_state.State.CurrentRunCount}.",
                classification: AuthorizationDecisionClassification.QuotaExceeded);
            CloudGrainsTelemetry.RecordAuthorizationDecision(quotaDecision.Code, quotaDecision.IsAuthorized);
            activity?.SetTag("ghost.authorization.code", quotaDecision.Code);
            activity?.SetTag("ghost.quota.limit", _state.State.DailyRunLimit);
            activity?.SetTag("ghost.quota.current", _state.State.CurrentRunCount);
            activity?.SetStatus(ActivityStatusCode.Error, quotaDecision.Message);
            await AppendAuditAsync(runId, endpointId, quotaDecision).ConfigureAwait(false);
            await _state.WriteStateAsync().ConfigureAwait(false);
            return quotaDecision;
        }

        // CL-002: Concurrent run limit with clear classification
        if (_state.State.ActiveRuns.Count >= _state.State.MaxConcurrentRuns)
        {
            RunAuthorizationDecision concurrentDecision = BuildDecision(
                isAuthorized: false,
                code: "MAX_CONCURRENT_RUNS_EXCEEDED",
                message: $"Tenant max concurrent run limit ({_state.State.MaxConcurrentRuns}) has been reached. Active runs: {_state.State.ActiveRuns.Count}.",
                classification: AuthorizationDecisionClassification.QuotaExceeded);
            CloudGrainsTelemetry.RecordAuthorizationDecision(concurrentDecision.Code, concurrentDecision.IsAuthorized);
            activity?.SetTag("ghost.authorization.code", concurrentDecision.Code);
            activity?.SetTag("ghost.concurrent.limit", _state.State.MaxConcurrentRuns);
            activity?.SetTag("ghost.concurrent.current", _state.State.ActiveRuns.Count);
            activity?.SetStatus(ActivityStatusCode.Error, concurrentDecision.Message);
            await AppendAuditAsync(runId, endpointId, concurrentDecision).ConfigureAwait(false);
            await _state.WriteStateAsync().ConfigureAwait(false);
            return concurrentDecision;
        }

        // Authorization successful
        _state.State.ActiveRuns.Add(runId);
        _state.State.CurrentRunCount++;
        RunAuthorizationDecision authorizedDecision = BuildDecision(
            isAuthorized: true,
            code: "AUTHORIZED",
            message: "Run authorized for execution.",
            classification: AuthorizationDecisionClassification.Authorized);
        CloudGrainsTelemetry.RecordAuthorizationDecision(authorizedDecision.Code, authorizedDecision.IsAuthorized);
        activity?.SetTag("ghost.authorization.code", authorizedDecision.Code);
        activity?.SetTag("ghost.run.count", _state.State.CurrentRunCount);
        activity?.SetStatus(ActivityStatusCode.Ok);
        await AppendAuditAsync(runId, endpointId, authorizedDecision).ConfigureAwait(false);
        await _state.WriteStateAsync().ConfigureAwait(false);

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

    private RunAuthorizationDecision BuildDecision(
        bool isAuthorized,
        string code,
        string message,
        AuthorizationDecisionClassification classification) => new()
    {
        IsAuthorized = isAuthorized,
        Code = code,
        Message = message,
        Classification = classification,
        CurrentRunCount = _state.State.CurrentRunCount,
        ActiveRunCount = _state.State.ActiveRuns.Count,
        DailyRunLimit = _state.State.DailyRunLimit,
        MaxConcurrentRuns = _state.State.MaxConcurrentRuns,
        TenantId = _state.State.TenantId,
        // CL-002: Generate verification evidence URI for denied requests
        VerificationEvidenceUri = isAuthorized
            ? null
            : $"ghost://audit/tenant/{_state.State.TenantId:N}/authorization/{Guid.NewGuid():N}"
    };

    private Task AppendAuditAsync(string runId, string endpointId, RunAuthorizationDecision decision)
    {
        // CL-002: Request correlation ID for end-to-end tracing
        string? correlationId = Activity.Current?.Id ?? Guid.NewGuid().ToString("N");

        var entry = new RunAuthorizationAuditEntry
        {
            RunId = runId,
            EndpointId = endpointId,
            Decision = decision,
            RequestCorrelationId = correlationId,
            RequestTimestamp = DateTimeOffset.UtcNow
        };

        _state.State.AuthorizationAudit.Add(entry);

        if (_state.State.AuthorizationAudit.Count > MaxAuditEntries)
        {
            _state.State.AuthorizationAudit.RemoveRange(
                0,
                _state.State.AuthorizationAudit.Count - MaxAuditEntries);
        }

        // Note: State is persisted by the caller (AuthorizeRunAsync) to batch writes
        return Task.CompletedTask;
    }
}
