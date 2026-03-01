using FluentAssertions;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Implementation;
using Ghost.Cloud.Grains.State;
using Ghost.Testing.Reliability;
using NSubstitute;
using Orleans.Runtime;
using Xunit.Abstractions;

namespace Ghost.Cloud.Grains.UnitTests;

public sealed class TenantGrainTests : ReliabilityTestBase
{
    public TenantGrainTests(ITestOutputHelper output) : base(output) { }
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

    // CL-002: Enhanced auditability tests

    [Fact]
    public async Task AuthorizeRunAsync_IncludesClassification_ForQuotaExceededAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 1,
            CurrentRunCount = 1,
            MaxConcurrentRuns = 5,
            TenantId = Guid.NewGuid()
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        RunAuthorizationDecision decision = await grain.AuthorizeRunAsync("run-1", "endpoint-1");

        decision.IsAuthorized.Should().BeFalse();
        decision.Classification.Should().Be(AuthorizationDecisionClassification.QuotaExceeded);
        decision.VerificationEvidenceUri.Should().NotBeNullOrEmpty();
        decision.VerificationEvidenceUri.Should().StartWith("ghost://audit/tenant/");
        decision.TenantId.Should().Be(state.TenantId);
    }

    [Fact]
    public async Task AuthorizeRunAsync_IncludesClassification_ForAuthorizedAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 100,
            CurrentRunCount = 0,
            MaxConcurrentRuns = 5,
            TenantId = Guid.NewGuid()
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        RunAuthorizationDecision decision = await grain.AuthorizeRunAsync("run-1", "endpoint-1");

        decision.IsAuthorized.Should().BeTrue();
        decision.Classification.Should().Be(AuthorizationDecisionClassification.Authorized);
        decision.VerificationEvidenceUri.Should().BeNull();
        decision.TenantId.Should().Be(state.TenantId);
    }

    [Fact]
    public async Task AuthorizeRunAsync_IncludesClassification_ForIdempotentAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 100,
            CurrentRunCount = 0,
            MaxConcurrentRuns = 5
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        RunAuthorizationDecision firstDecision = await grain.AuthorizeRunAsync("run-1", "endpoint-1");
        RunAuthorizationDecision secondDecision = await grain.AuthorizeRunAsync("run-1", "endpoint-1");

        firstDecision.Classification.Should().Be(AuthorizationDecisionClassification.Authorized);
        secondDecision.Classification.Should().Be(AuthorizationDecisionClassification.Idempotent);
    }

    [Fact]
    public async Task AuthorizeRunAsync_RecordsAuditEntry_WithCorrelationIdAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 100,
            CurrentRunCount = 0,
            MaxConcurrentRuns = 5
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        await grain.AuthorizeRunAsync("run-1", "endpoint-1");

        state.AuthorizationAudit.Should().ContainSingle();
        state.AuthorizationAudit[0].RequestCorrelationId.Should().NotBeNullOrEmpty();
        state.AuthorizationAudit[0].RequestTimestamp.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task AuthorizeRunAsync_Denies_WithValidationFailed_ForEmptyRunIdAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 100,
            CurrentRunCount = 0,
            MaxConcurrentRuns = 5
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        RunAuthorizationDecision decision = await grain.AuthorizeRunAsync("", "endpoint-1");

        decision.IsAuthorized.Should().BeFalse();
        decision.Code.Should().Be("VALIDATION_FAILED");
        decision.Classification.Should().Be(AuthorizationDecisionClassification.ValidationFailed);
    }

    [Fact]
    public async Task AuthorizeRunAsync_Denies_WithValidationFailed_ForEmptyEndpointIdAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 100,
            CurrentRunCount = 0,
            MaxConcurrentRuns = 5
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        RunAuthorizationDecision decision = await grain.AuthorizeRunAsync("run-1", "");

        decision.IsAuthorized.Should().BeFalse();
        decision.Code.Should().Be("VALIDATION_FAILED");
        decision.Classification.Should().Be(AuthorizationDecisionClassification.ValidationFailed);
    }

    [Fact]
    public async Task GetAuthorizationAuditAsync_ReturnsAuditEntriesAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 100,
            CurrentRunCount = 0,
            MaxConcurrentRuns = 5,
            AuthorizationAudit =
            [
                new RunAuthorizationAuditEntry
                {
                    RunId = "run-1",
                    EndpointId = "endpoint-1",
                    Decision = new RunAuthorizationDecision { IsAuthorized = true, Code = "AUTHORIZED" },
                    RequestCorrelationId = "corr-1",
                    RequestTimestamp = DateTimeOffset.UtcNow.AddMinutes(-5)
                },
                new RunAuthorizationAuditEntry
                {
                    RunId = "run-2",
                    EndpointId = "endpoint-2",
                    Decision = new RunAuthorizationDecision { IsAuthorized = false, Code = "DAILY_QUOTA_EXCEEDED" },
                    RequestCorrelationId = "corr-2",
                    RequestTimestamp = DateTimeOffset.UtcNow.AddMinutes(-3)
                }
            ]
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        IReadOnlyList<RunAuthorizationAuditEntry> audit = await grain.GetAuthorizationAuditAsync(10);

        audit.Should().HaveCount(2);
        audit[0].RunId.Should().Be("run-1");
        audit[1].RunId.Should().Be("run-2");
    }

    [Fact]
    public async Task IsRunAllowedAsync_RespectsDailyLimitAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 5,
            CurrentRunCount = 4,
            MaxConcurrentRuns = 10
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        bool allowed = await grain.IsRunAllowedAsync("endpoint-1");

        allowed.Should().BeTrue();
    }

    [Fact]
    public async Task IsRunAllowedAsync_ReturnsFalse_WhenDailyLimitExceededAsync()
    {
        TenantState state = new()
        {
            DailyRunLimit = 5,
            CurrentRunCount = 5,
            MaxConcurrentRuns = 10
        };

        IPersistentState<TenantState> persistentState = CreatePersistentState(state);
        TenantGrain grain = new(persistentState);

        bool allowed = await grain.IsRunAllowedAsync("endpoint-1");

        allowed.Should().BeFalse();
    }

    private static IPersistentState<TenantState> CreatePersistentState(TenantState state)
    {
        IPersistentState<TenantState> persistentState = Substitute.For<IPersistentState<TenantState>>();
        persistentState.State.Returns(state);
        persistentState.WriteStateAsync().Returns(Task.CompletedTask);
        return persistentState;
    }
}
