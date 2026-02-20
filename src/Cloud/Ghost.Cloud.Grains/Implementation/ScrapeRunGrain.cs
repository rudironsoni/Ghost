using System.Diagnostics;
using Ghost.Cloud.Contracts.Events;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Grains.Observability;
using Ghost.Cloud.Grains.State;
using Orleans.EventSourcing;

namespace Ghost.Cloud.Grains.Implementation;

public sealed class ScrapeRunGrain : JournaledGrain<ScrapeRunState, ScrapeRunEvent>, IScrapeRunGrain
{
    public async Task<ScrapeRunStatus> TriggerAsync(ScrapeRunRequest request)
    {
        using Activity? activity = CloudGrainsTelemetry.ActivitySource.StartActivity("CloudGrains.ScrapeRun.Trigger");
        activity?.SetTag("ghost.run.id", this.GetPrimaryKeyString());
        activity?.SetTag("ghost.endpoint.id", request.EndpointId);
        activity?.SetTag("ghost.mode", request.RequestedMode);
        CloudGrainsTelemetry.RecordRunTrigger(request.RequestedMode);

        Guid tenantId = request.TenantId;
        activity?.SetTag("ghost.tenant.id", tenantId);
        if (tenantId == Guid.Empty)
        {
            CloudGrainsTelemetry.RecordRunTriggerFailure("TENANT_REQUIRED", request.RequestedMode);
            activity?.SetStatus(ActivityStatusCode.Error, "TenantId must be a non-empty GUID.");
            throw new ArgumentException("TenantId must be a non-empty GUID.", nameof(request));
        }

        ITenantGrain tenantGrain = GrainFactory.GetGrain<ITenantGrain>(tenantId);

        RunAuthorizationDecision authorizationDecision = await tenantGrain
            .AuthorizeRunAsync(this.GetPrimaryKeyString(), request.EndpointId)
            .ConfigureAwait(false);
        if (!authorizationDecision.IsAuthorized)
        {
            CloudGrainsTelemetry.RecordRunTriggerFailure(authorizationDecision.Code, request.RequestedMode);
            activity?.SetTag("ghost.authorization.code", authorizationDecision.Code);
            activity?.SetStatus(ActivityStatusCode.Error, authorizationDecision.Message);
            RaiseEvent(new ScrapeRunFailed(
                this.GetPrimaryKeyString(),
                authorizationDecision.Code,
                authorizationDecision.Message,
                false,
                DateTimeOffset.UtcNow));
            return MapToStatus(State);
        }

        RaiseEvent(new ScrapeRunTriggered(
            this.GetPrimaryKeyString(),
            request.EndpointId,
            tenantId,
            request.RequestedMode,
            DateTimeOffset.UtcNow));

        activity?.SetStatus(ActivityStatusCode.Ok);
        State.DeliveryConfig = request.Delivery;
        return MapToStatus(State);
    }

    public Task StartAsync(string workerId)
    {
        RaiseEvent(new ScrapeRunStarted(
            this.GetPrimaryKeyString(),
            workerId,
            DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task ReportItemAsync(string itemId, JsonElement data)
    {
        RaiseEvent(new ItemDiscovered(
            this.GetPrimaryKeyString(),
            itemId,
            data,
            DateTimeOffset.UtcNow));

        return StreamItemAsync(data);
    }

    public Task ReportArtifactAsync(string itemId, string artifactType, string storageUri, string hash)
    {
        RaiseEvent(new ArtifactCaptured(
            this.GetPrimaryKeyString(),
            itemId,
            artifactType,
            storageUri,
            hash,
            DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }

    public Task CompleteAsync(int itemsDiscovered, int artifactsCaptured)
    {
        using Activity? activity = CloudGrainsTelemetry.ActivitySource.StartActivity("CloudGrains.ScrapeRun.Complete");
        activity?.SetTag("ghost.run.id", this.GetPrimaryKeyString());
        activity?.SetTag("ghost.items.discovered", itemsDiscovered);
        activity?.SetTag("ghost.artifacts.captured", artifactsCaptured);

        RaiseEvent(new ScrapeRunCompleted(
            this.GetPrimaryKeyString(),
            itemsDiscovered,
            artifactsCaptured,
            DateTimeOffset.UtcNow));

        if (State.StartedAt != DateTimeOffset.MinValue)
        {
            double durationSeconds = (DateTimeOffset.UtcNow - State.StartedAt).TotalSeconds;
            CloudGrainsTelemetry.RecordRunDuration(durationSeconds, "completed");
            activity?.SetTag("ghost.run.duration.seconds", durationSeconds);
        }

        activity?.SetStatus(ActivityStatusCode.Ok);

        _ = Task.Run(async () =>
        {
            ITenantGrain tenantGrain = GrainFactory.GetGrain<ITenantGrain>(State.TenantId);
            await tenantGrain.RecordUsageAsync(
                this.GetPrimaryKeyString(),
                itemsDiscovered,
                (long)(DateTimeOffset.UtcNow - State.StartedAt).TotalMilliseconds).ConfigureAwait(false);
        });

        return Task.CompletedTask;
    }

    public Task FailAsync(string errorCode, string errorMessage, bool retryable)
    {
        using Activity? activity = CloudGrainsTelemetry.ActivitySource.StartActivity("CloudGrains.ScrapeRun.Fail");
        activity?.SetTag("ghost.run.id", this.GetPrimaryKeyString());
        activity?.SetTag("ghost.error.code", errorCode);
        activity?.SetTag("ghost.retryable", retryable);
        activity?.SetStatus(ActivityStatusCode.Error, errorMessage);
        CloudGrainsTelemetry.RecordRunTriggerFailure(errorCode, "runtime");

        RaiseEvent(new ScrapeRunFailed(
            this.GetPrimaryKeyString(),
            errorCode,
            errorMessage,
            retryable,
            DateTimeOffset.UtcNow));

        if (State.StartedAt != DateTimeOffset.MinValue)
        {
            double durationSeconds = (DateTimeOffset.UtcNow - State.StartedAt).TotalSeconds;
            CloudGrainsTelemetry.RecordRunDuration(durationSeconds, "failed");
        }

        return Task.CompletedTask;
    }

    public Task<ScrapeRunStatus> GetStatusAsync() => Task.FromResult(MapToStatus(State));

    public Task CancelAsync() => throw new NotImplementedException();

    private static Task StreamItemAsync(JsonElement data)
    {
        // Streaming implementation - would use Orleans streams with proper provider setup
        // For now, this is a no-op as results are pulled via API
        return Task.CompletedTask;
    }

    protected override void TransitionState(ScrapeRunState state, ScrapeRunEvent @event) => state.Apply(@event);

    private static ScrapeRunStatus MapToStatus(ScrapeRunState state) => new()
    {
        RunId = state.RunId,
        EndpointId = state.EndpointId,
        Status = state.Status,
        ItemsDiscovered = state.ItemsDiscovered,
        ItemsDelivered = state.ItemsDiscovered,
        StartedAt = state.StartedAt,
        CompletedAt = state.CompletedAt,
        ErrorMessage = state.ErrorMessage,
        ErrorCode = state.ErrorCode
    };
}
