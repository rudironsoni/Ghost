using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Implementation;
using NSubstitute;
using Orleans.Runtime;

namespace Ghost.Cloud.Grains.UnitTests;

public sealed class SchedulerGrainTests
{
    [Fact]
    public async Task ScheduleRunAsync_PersistsCanaryMetadataAsync()
    {
        SchedulerState schedulerState = new();
        IPersistentState<SchedulerState> persistentState = CreatePersistentState(schedulerState);
        SchedulerGrain grain = new(persistentState);

        await grain.ScheduleRunAsync(new ScheduledRunRequest
        {
            RunId = "run-1",
            EndpointId = "endpoint-1",
            TenantId = Guid.NewGuid(),
            ScheduledTime = DateTimeOffset.UtcNow.AddMinutes(5),
            RunKind = "canary",
            RequestedMode = "canary"
        });

        ScheduledRun scheduledRun = schedulerState.ScheduledRuns["run-1"];
        scheduledRun.RunKind.Should().Be("canary");
        scheduledRun.EndpointId.Should().Be("endpoint-1");
        scheduledRun.Status.Should().Be("Pending");
        await persistentState.Received(1).WriteStateAsync();
    }

    [Fact]
    public async Task ScheduleRunAsync_WithEmptyTenantId_ThrowsAndDoesNotPersistAsync()
    {
        SchedulerState schedulerState = new();
        IPersistentState<SchedulerState> persistentState = CreatePersistentState(schedulerState);
        SchedulerGrain grain = new(persistentState);

        Func<Task> act = () => grain.ScheduleRunAsync(new ScheduledRunRequest
        {
            RunId = "run-empty-tenant",
            EndpointId = "endpoint-1",
            TenantId = Guid.Empty,
            ScheduledTime = DateTimeOffset.UtcNow.AddMinutes(1),
            RunKind = "canary",
            RequestedMode = "canary"
        });

        await act.Should().ThrowAsync<ArgumentException>();
        await persistentState.DidNotReceive().WriteStateAsync();
    }

    [Fact]
    public async Task GetDueRunsAsync_ReturnsDueRunsAndMarksDispatchingAsync()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        SchedulerState schedulerState = new();
        schedulerState.ScheduledRuns["due"] = new ScheduledRun
        {
            RunId = "due",
            EndpointId = "endpoint-due",
            TenantId = Guid.NewGuid(),
            ScheduledTime = now.AddSeconds(-1),
            Status = "Pending",
            RunKind = "canary",
            RequestedMode = "canary"
        };
        schedulerState.ScheduledRuns["future"] = new ScheduledRun
        {
            RunId = "future",
            EndpointId = "endpoint-future",
            TenantId = Guid.NewGuid(),
            ScheduledTime = now.AddMinutes(10),
            Status = "Pending",
            RunKind = "canary",
            RequestedMode = "canary"
        };

        IPersistentState<SchedulerState> persistentState = CreatePersistentState(schedulerState);
        SchedulerGrain grain = new(persistentState);

        List<ScheduledRunInfo> dueRuns = await grain.GetDueRunsAsync(now, maxCount: 10);

        dueRuns.Should().ContainSingle(run => run.RunId == "due");
        schedulerState.ScheduledRuns["due"].Status.Should().Be("Dispatching");
        schedulerState.ScheduledRuns["future"].Status.Should().Be("Pending");
        await persistentState.Received(1).WriteStateAsync();
    }

    [Fact]
    public async Task MarkRunStatusAsync_UpdatesClassificationAndDiagnosticsAsync()
    {
        SchedulerState schedulerState = new();
        schedulerState.ScheduledRuns["run-2"] = new ScheduledRun
        {
            RunId = "run-2",
            EndpointId = "endpoint-2",
            TenantId = Guid.NewGuid(),
            ScheduledTime = DateTimeOffset.UtcNow,
            Status = "Dispatching",
            RunKind = "canary",
            RequestedMode = "canary"
        };

        IPersistentState<SchedulerState> persistentState = CreatePersistentState(schedulerState);
        SchedulerGrain grain = new(persistentState);

        await grain.MarkRunStatusAsync("run-2", "Failed", "RateLimited", "s3://diag/run-2");

        ScheduledRun updatedRun = schedulerState.ScheduledRuns["run-2"];
        updatedRun.Status.Should().Be("Failed");
        updatedRun.Classification.Should().Be("RateLimited");
        updatedRun.DiagnosticsUri.Should().Be("s3://diag/run-2");
        await persistentState.Received(1).WriteStateAsync();
    }

    private static IPersistentState<SchedulerState> CreatePersistentState(SchedulerState state)
    {
        IPersistentState<SchedulerState> persistentState = Substitute.For<IPersistentState<SchedulerState>>();
        persistentState.State.Returns(state);
        persistentState.WriteStateAsync().Returns(Task.CompletedTask);
        return persistentState;
    }
}
