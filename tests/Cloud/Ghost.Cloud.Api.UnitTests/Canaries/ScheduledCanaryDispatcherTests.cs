using Ghost.Cloud.Api.Canaries;
using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit.Abstractions;

namespace Ghost.Cloud.Api.UnitTests.Canaries;

public sealed class ScheduledCanaryDispatcherTests : ReliabilityTestBase
{
    public ScheduledCanaryDispatcherTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task DispatchDueCanariesOnceAsync_CompletesRun_WhenRunnerSucceedsAsync()
    {
        IClusterClient clusterClient = Substitute.For<IClusterClient>();
        ISchedulerGrain scheduler = Substitute.For<ISchedulerGrain>();
        IScrapeRunGrain runGrain = Substitute.For<IScrapeRunGrain>();
        IAssuranceCanaryRunner canaryRunner = Substitute.For<IAssuranceCanaryRunner>();

        ScheduledRunInfo dueRun = CreateCanaryRun("run-success");
        scheduler.GetDueRunsAsync(Arg.Any<DateTimeOffset>(), 20).Returns([dueRun]);
        clusterClient.GetGrain<ISchedulerGrain>("default", null).Returns(scheduler);
        clusterClient.GetGrain<IScrapeRunGrain>("run-success", null).Returns(runGrain);
        runGrain.TriggerAsync(Arg.Any<ScrapeRunRequest>()).Returns(new ScrapeRunStatus { Status = "Pending" });
        canaryRunner.RunAsync(dueRun, Arg.Any<CancellationToken>()).Returns(new CanaryRunOutcome
        {
            Success = true,
            Classification = "Success",
            DiagnosticsUri = "s3://diagnostics/run-success",
            ItemsDiscovered = 3,
            ArtifactsCaptured = 1
        });

        ScheduledCanaryDispatcher dispatcher = new(clusterClient, canaryRunner, NullLogger<ScheduledCanaryDispatcher>.Instance);

        await dispatcher.DispatchDueCanariesOnceAsync(CancellationToken.None);

        await runGrain.Received(1).StartAsync("cloud-canary-dispatcher");
        await runGrain.Received(1).CompleteAsync(3, 1);
        await scheduler
            .Received(1)
            .MarkRunStatusAsync("run-success", "Completed", "Success", "s3://diagnostics/run-success");
    }

    [Fact]
    public async Task DispatchDueCanariesOnceAsync_FailsRun_WhenRunnerFailsAsync()
    {
        IClusterClient clusterClient = Substitute.For<IClusterClient>();
        ISchedulerGrain scheduler = Substitute.For<ISchedulerGrain>();
        IScrapeRunGrain runGrain = Substitute.For<IScrapeRunGrain>();
        IAssuranceCanaryRunner canaryRunner = Substitute.For<IAssuranceCanaryRunner>();

        ScheduledRunInfo dueRun = CreateCanaryRun("run-failure");
        scheduler.GetDueRunsAsync(Arg.Any<DateTimeOffset>(), 20).Returns([dueRun]);
        clusterClient.GetGrain<ISchedulerGrain>("default", null).Returns(scheduler);
        clusterClient.GetGrain<IScrapeRunGrain>("run-failure", null).Returns(runGrain);
        runGrain.TriggerAsync(Arg.Any<ScrapeRunRequest>()).Returns(new ScrapeRunStatus { Status = "Pending" });
        canaryRunner.RunAsync(dueRun, Arg.Any<CancellationToken>()).Returns(new CanaryRunOutcome
        {
            Success = false,
            Classification = "RateLimited",
            ErrorMessage = "429 from provider"
        });

        ScheduledCanaryDispatcher dispatcher = new(clusterClient, canaryRunner, NullLogger<ScheduledCanaryDispatcher>.Instance);

        await dispatcher.DispatchDueCanariesOnceAsync(CancellationToken.None);

        await runGrain.Received(1).FailAsync("RateLimited", "429 from provider", false);
        await scheduler.Received(1).MarkRunStatusAsync("run-failure", "Failed", "RateLimited", null);
    }

    private static ScheduledRunInfo CreateCanaryRun(string runId) => new()
    {
        RunId = runId,
        EndpointId = "endpoint-1",
        TenantId = Guid.NewGuid(),
        ScheduledTime = DateTimeOffset.UtcNow.AddSeconds(-10),
        Status = "Dispatching",
        RunKind = "canary",
        RequestedMode = "canary"
    };
}
