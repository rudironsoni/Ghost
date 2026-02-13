namespace Ghost.Plugin.Indeed;

public sealed class IndeedPluginOptions
{
    public bool UsePluginRuntime { get; init; } = true;

    public bool RegisterReadinessServices { get; init; } = true;

    public bool RegisterKeyedJobClient { get; init; } = true;
}
