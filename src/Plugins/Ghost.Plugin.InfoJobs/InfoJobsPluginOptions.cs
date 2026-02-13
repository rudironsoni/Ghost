namespace Ghost.Plugin.InfoJobs;

public sealed class InfoJobsPluginOptions
{
    public bool UsePluginRuntime { get; init; } = true;

    public bool RegisterReadinessServices { get; init; } = true;

    public bool RegisterKeyedJobClient { get; init; } = true;
}
