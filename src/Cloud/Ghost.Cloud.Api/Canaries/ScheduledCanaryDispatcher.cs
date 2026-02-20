using Ghost.Cloud.Contracts.Runs;
using Ghost.Cloud.Grains.Interfaces;

namespace Ghost.Cloud.Api.Canaries;

public sealed class ScheduledCanaryDispatcher : BackgroundService
{
    private const string SchedulerGrainKey = "default";
    private static readonly Action<ILogger, Exception?> LogDispatchLoopFailed =
        LoggerMessage.Define(
            LogLevel.Error,
            new EventId(1001, nameof(LogDispatchLoopFailed)),
            "Failed to dispatch scheduled canary runs.");
    private static readonly Action<ILogger, string, Exception?> LogDispatchRunFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(1002, nameof(LogDispatchRunFailed)),
            "Canary dispatch failed for run {RunId}.");

    private readonly IClusterClient _clusterClient;
    private readonly IAssuranceCanaryRunner _canaryRunner;
    private readonly ILogger<ScheduledCanaryDispatcher> _logger;

    public ScheduledCanaryDispatcher(
        IClusterClient clusterClient,
        IAssuranceCanaryRunner canaryRunner,
        ILogger<ScheduledCanaryDispatcher> logger)
    {
        _clusterClient = clusterClient;
        _canaryRunner = canaryRunner;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await DispatchDueCanariesOnceAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogDispatchLoopFailed(_logger, ex);
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken).ConfigureAwait(false);
        }
    }

    public async Task DispatchDueCanariesOnceAsync(CancellationToken cancellationToken)
    {
        ISchedulerGrain scheduler = _clusterClient.GetGrain<ISchedulerGrain>(SchedulerGrainKey);
        List<ScheduledRunInfo> dueRuns = await scheduler
            .GetDueRunsAsync(DateTimeOffset.UtcNow, maxCount: 20)
            .ConfigureAwait(false);

        foreach (ScheduledRunInfo scheduledRun in dueRuns)
        {
            if (!string.Equals(scheduledRun.RunKind, "canary", StringComparison.OrdinalIgnoreCase))
            {
                await scheduler
                    .MarkRunStatusAsync(
                        scheduledRun.RunId,
                        "Skipped",
                        "UnsupportedRunKind",
                        null)
                    .ConfigureAwait(false);
                continue;
            }

            await ExecuteCanaryRunAsync(scheduledRun, scheduler, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ExecuteCanaryRunAsync(
        ScheduledRunInfo scheduledRun,
        ISchedulerGrain scheduler,
        CancellationToken cancellationToken)
    {
        IScrapeRunGrain runGrain = _clusterClient.GetGrain<IScrapeRunGrain>(scheduledRun.RunId);

        try
        {
            ScrapeRunStatus status = await runGrain.TriggerAsync(new ScrapeRunRequest
            {
                EndpointId = scheduledRun.EndpointId,
                Input = scheduledRun.Input,
                RequestedMode = scheduledRun.RequestedMode,
                TenantId = scheduledRun.TenantId
            }).ConfigureAwait(false);

            if (string.Equals(status.Status, "Failed", StringComparison.OrdinalIgnoreCase))
            {
                await scheduler
                    .MarkRunStatusAsync(
                        scheduledRun.RunId,
                        "Failed",
                        "TriggerFailed",
                        null)
                    .ConfigureAwait(false);
                return;
            }

            await runGrain.StartAsync("cloud-canary-dispatcher").ConfigureAwait(false);
            CanaryRunOutcome outcome = await _canaryRunner.RunAsync(scheduledRun, cancellationToken).ConfigureAwait(false);

            if (outcome.Success)
            {
                await runGrain
                    .CompleteAsync(outcome.ItemsDiscovered, outcome.ArtifactsCaptured)
                    .ConfigureAwait(false);
                await scheduler
                    .MarkRunStatusAsync(
                        scheduledRun.RunId,
                        "Completed",
                        outcome.Classification,
                        outcome.DiagnosticsUri)
                    .ConfigureAwait(false);
                return;
            }

            await runGrain
                .FailAsync(
                    outcome.Classification,
                    outcome.ErrorMessage ?? "Canary run failed.",
                    retryable: false)
                .ConfigureAwait(false);
            await scheduler
                .MarkRunStatusAsync(
                    scheduledRun.RunId,
                    "Failed",
                    outcome.Classification,
                    outcome.DiagnosticsUri)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await runGrain
                .FailAsync("DispatchError", ex.Message, retryable: true)
                .ConfigureAwait(false);
            await scheduler
                .MarkRunStatusAsync(
                    scheduledRun.RunId,
                    "Failed",
                    "DispatchError",
                    null)
                .ConfigureAwait(false);
            LogDispatchRunFailed(_logger, scheduledRun.RunId, ex);
        }
    }
}
