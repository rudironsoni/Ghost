using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Implementation;
using Ghost.Cloud.Grains.State;
using NSubstitute;
using Orleans.Runtime;

namespace Ghost.Cloud.Grains.UnitTests;

public sealed class TenantGrainTests
{
    [Fact]
    public async Task AuthorizeRunAsync_Denies_WhenDailyLimitExceededAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 1,
            CurrentRunCount = 1,
            MaxConcurrentRuns = 5
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        RunAuthorizationDecision decision = await grain.AuthorizeRunAsync("run-1", "endpoint-1");

        decision.IsAuthorized.Should().BeFalse();
        decision.Code.Should().Be("DAILY_QUOTA_EXCEEDED");
        decision.Message.Should().NotBeNullOrWhiteSpace();
        state.AuthorizationAudit.Should().ContainSingle();
        await persistentState.Received(1).WriteStateAsync();
    }

    [Fact]
    public async Task AuthorizeRunAsync_Denies_WhenMaxConcurrentRunsExceededAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 100,
            CurrentRunCount = 10,
            MaxConcurrentRuns = 1,
            ActiveRuns = ["existing-run"]
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        RunAuthorizationDecision decision = await grain.AuthorizeRunAsync("run-2", "endpoint-1");

        decision.IsAuthorized.Should().BeFalse();
        decision.Code.Should().Be("MAX_CONCURRENT_RUNS_EXCEEDED");
        state.ActiveRuns.Should().ContainSingle();
        await persistentState.Received(1).WriteStateAsync();
    }

    [Fact]
    public async Task AuthorizeRunAsync_IsIdempotent_ForSameRunIdAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 100,
            CurrentRunCount = 0,
            MaxConcurrentRuns = 5
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        RunAuthorizationDecision firstDecision = await grain.AuthorizeRunAsync("run-3", "endpoint-1");
        RunAuthorizationDecision secondDecision = await grain.AuthorizeRunAsync("run-3", "endpoint-1");

        firstDecision.IsAuthorized.Should().BeTrue();
        firstDecision.Code.Should().Be("AUTHORIZED");
        secondDecision.IsAuthorized.Should().BeTrue();
        secondDecision.Code.Should().Be("ALREADY_AUTHORIZED");
        state.CurrentRunCount.Should().Be(1);
        state.ActiveRuns.Should().ContainSingle(runId => runId == "run-3");
        await persistentState.Received(2).WriteStateAsync();
    }

    [Fact]
    public async Task RecordUsageAsync_RemovesRunFromActiveSetAndPersistsAsync()
    {
        TenantState state = new()
        {
            ActiveRuns = ["run-4"]
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        await grain.RecordUsageAsync("run-4", itemsScraped: 10, executionTimeMs: 1000);

        state.ActiveRuns.Should().NotContain("run-4");
        await persistentState.Received(1).WriteStateAsync();
    }

    private static IPersistentState<TenantState> CreatePersistentState(TenantState state)
    {
        IPersistentState<TenantState> persistentState = Substitute.For<IPersistentState<TenantState>>();
        persistentState.State.Returns(state);
        persistentState.WriteStateAsync().Returns(Task.CompletedTask);
        return persistentState;
    }
}
