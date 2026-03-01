using System.Diagnostics;
using Ghost.Cloud.Contracts.Events;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Grains.Observability;
using Ghost.Cloud.Grains.State;

namespace Ghost.Cloud.Grains.Implementation;

/// <summary>
/// Scheduler grain for managing scheduled runs with support for one-time and recurring schedules.
/// </summary>
public sealed class SchedulerGrain : Grain, ISchedulerGrain
{
    private readonly IPersistentState<SchedulerState> _state;

    public SchedulerGrain([PersistentState("scheduler", "Default")] IPersistentState<SchedulerState> state)
    {
        _state = state;
    }

    public Task ScheduleRunAsync(ScheduledRunRequest request)
    {
        using Activity? activity = CloudGrainsTelemetry.ActivitySource.StartActivity("CloudGrains.Scheduler.ScheduleRun");
        activity?.SetTag("ghost.run.id", request.RunId);
        activity?.SetTag("ghost.endpoint.id", request.EndpointId);
        activity?.SetTag("ghost.run.kind", request.RunKind);
        activity?.SetTag("ghost.schedule.type", request.ScheduleType.ToString());

        ArgumentNullException.ThrowIfNull(request);
        if (request.TenantId == Guid.Empty)
        {
            CloudGrainsTelemetry.RecordScheduledOperation("schedule", "rejected");
            activity?.SetStatus(ActivityStatusCode.Error, "TenantId must be a non-empty GUID.");
            throw new ArgumentException("TenantId must be a non-empty GUID.", nameof(request));
        }

        // CL-003: Validate RunKind
        if (!IsValidRunKind(request.RunKind))
        {
            CloudGrainsTelemetry.RecordScheduledOperation("schedule", "rejected");
            activity?.SetStatus(ActivityStatusCode.Error, $"Invalid RunKind: {request.RunKind}");
            throw new ArgumentException($"Invalid RunKind: {request.RunKind}. Valid values are: canary, cassette-refresh, replay", nameof(request));
        }

        // CL-003: Validate recurring schedule if provided
        if (request.ScheduleType == ScheduleType.Recurring && request.RecurringSchedule is not null)
        {
            if (!TryValidateRecurringSchedule(request.RecurringSchedule, out string? validationError))
            {
                CloudGrainsTelemetry.RecordScheduledOperation("schedule", "rejected");
                activity?.SetStatus(ActivityStatusCode.Error, validationError);
                throw new ArgumentException(validationError, nameof(request));
            }
        }

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
            // CL-003: Store recurring schedule configuration
            ScheduleType = request.ScheduleType,
            RecurringSchedule = request.RecurringSchedule,
            OccurrenceCount = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _state.State.ScheduledRuns[request.RunId] = scheduledRun;
        CloudGrainsTelemetry.RecordScheduledOperation("schedule", "accepted");
        activity?.SetStatus(ActivityStatusCode.Ok);
        return _state.WriteStateAsync();
    }

    /// <summary>
    /// Validates that the run kind is one of the supported values.
    /// </summary>
    private static bool IsValidRunKind(string runKind)
    {
        return runKind.Equals("canary", StringComparison.OrdinalIgnoreCase) ||
               runKind.Equals("cassette-refresh", StringComparison.OrdinalIgnoreCase) ||
               runKind.Equals("replay", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates a recurring schedule configuration.
    /// </summary>
    private static bool TryValidateRecurringSchedule(RecurringSchedule schedule, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(schedule.CronExpression))
        {
            error = "CronExpression is required for recurring schedules.";
            return false;
        }

        // Basic cron validation (should have 5 fields: minute hour day month dayOfWeek)
        string[] parts = schedule.CronExpression.Split(' ');
        if (parts.Length != 5)
        {
            error = "CronExpression must have 5 fields (minute hour day month dayOfWeek).";
            return false;
        }

        // Validate time zone
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            error = $"Invalid TimeZoneId: {schedule.TimeZoneId}";
            return false;
        }

        // Validate max occurrences
        if (schedule.MaxOccurrences.HasValue && schedule.MaxOccurrences.Value <= 0)
        {
            error = "MaxOccurrences must be greater than 0.";
            return false;
        }

        return true;
    }

    public Task CancelScheduledRunAsync(string runId)
    {
        using Activity? activity = CloudGrainsTelemetry.ActivitySource.StartActivity("CloudGrains.Scheduler.CancelRun");
        activity?.SetTag("ghost.run.id", runId);

        if (_state.State.ScheduledRuns.TryGetValue(runId, out ScheduledRun? run))
        {
            run.Status = "Cancelled";
            run.UpdatedAt = DateTimeOffset.UtcNow;
            CloudGrainsTelemetry.RecordScheduledOperation("cancel", "updated");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return _state.WriteStateAsync();
        }

        CloudGrainsTelemetry.RecordScheduledOperation("cancel", "not_found");
        activity?.SetStatus(ActivityStatusCode.Ok);
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
        using Activity? activity = CloudGrainsTelemetry.ActivitySource.StartActivity("CloudGrains.Scheduler.GetDueRuns");
        activity?.SetTag("ghost.max.count", maxCount);

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
            CloudGrainsTelemetry.RecordScheduledOperation("due_runs", "dispatching");
            await _state.WriteStateAsync().ConfigureAwait(false);
        }

        activity?.SetTag("ghost.due.count", dueRuns.Count);
        activity?.SetStatus(ActivityStatusCode.Ok);
        return dueRuns.Select(run => run.ToInfo()).ToList();
    }

    public Task MarkRunStatusAsync(string runId, string status, string? classification, string? diagnosticsUri)
    {
        using Activity? activity = CloudGrainsTelemetry.ActivitySource.StartActivity("CloudGrains.Scheduler.MarkRunStatus");
        activity?.SetTag("ghost.run.id", runId);
        activity?.SetTag("ghost.status", status);
        activity?.SetTag("ghost.classification", classification);

        if (!_state.State.ScheduledRuns.TryGetValue(runId, out ScheduledRun? run))
        {
            CloudGrainsTelemetry.RecordScheduledOperation("mark_status", "not_found");
            activity?.SetStatus(ActivityStatusCode.Ok);
            return Task.CompletedTask;
        }

        run.Status = status;
        run.Classification = classification;
        run.DiagnosticsUri = diagnosticsUri;
        run.UpdatedAt = DateTimeOffset.UtcNow;
        CloudGrainsTelemetry.RecordScheduledOperation("mark_status", status);
        activity?.SetStatus(ActivityStatusCode.Ok);

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

    // CL-003: Recurring schedule support
    [Id(11)] public ScheduleType ScheduleType { get; set; } = ScheduleType.OneTime;
    [Id(12)] public RecurringSchedule? RecurringSchedule { get; set; }
    [Id(13)] public int OccurrenceCount { get; set; }
    [Id(14)] public string? ParentScheduleId { get; set; }

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
        UpdatedAt = UpdatedAt,
        ScheduleType = ScheduleType,
        RecurringSchedule = RecurringSchedule,
        OccurrenceCount = OccurrenceCount
    };
}
