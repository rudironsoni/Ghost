using Ghost.Cloud.Contracts.Events;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Cloud.Grains.State;
using Orleans.EventSourcing;

namespace Ghost.Cloud.Grains.Implementation;

public sealed class ScrapeRunGrain : JournaledGrain<ScrapeRunState, ScrapeRunEvent>, IScrapeRunGrain
{
    public async Task<ScrapeRunStatus> TriggerAsync(ScrapeRunRequest request)
    {
        Guid tenantId = GetTenantIdFromContext();
        ITenantGrain tenantGrain = GrainFactory.GetGrain<ITenantGrain>(tenantId);

        bool authorized = await tenantGrain.AuthorizeRunAsync(this.GetPrimaryKeyString(), request.EndpointId).ConfigureAwait(false);
        if (!authorized)
        {
            RaiseEvent(new ScrapeRunFailed(
                this.GetPrimaryKeyString(),
                "QUOTA_EXCEEDED",
                "Tenant has exceeded quota for this endpoint",
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
        RaiseEvent(new ScrapeRunCompleted(
            this.GetPrimaryKeyString(),
            itemsDiscovered,
            artifactsCaptured,
            DateTimeOffset.UtcNow));

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
        RaiseEvent(new ScrapeRunFailed(
            this.GetPrimaryKeyString(),
            errorCode,
            errorMessage,
            retryable,
            DateTimeOffset.UtcNow));
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
        ErrorMessage = state.ErrorMessage
    };

    private static Guid GetTenantIdFromContext() => Guid.Empty;
}
