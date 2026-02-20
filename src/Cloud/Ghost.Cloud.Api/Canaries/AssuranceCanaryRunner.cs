using Ghost.Cloud.Contracts.Runs;

namespace Ghost.Cloud.Api.Canaries;

public interface IAssuranceCanaryRunner
{
    public Task<CanaryRunOutcome> RunAsync(ScheduledRunInfo scheduledRun, CancellationToken cancellationToken);
}

public sealed class AssuranceCanaryRunner : IAssuranceCanaryRunner
{
    public Task<CanaryRunOutcome> RunAsync(ScheduledRunInfo scheduledRun, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scheduledRun);

        CanaryRunOutcome outcome = new()
        {
            Success = true,
            Classification = "Success",
            ItemsDiscovered = 0,
            ArtifactsCaptured = 0
        };

        return Task.FromResult(outcome);
    }
}
