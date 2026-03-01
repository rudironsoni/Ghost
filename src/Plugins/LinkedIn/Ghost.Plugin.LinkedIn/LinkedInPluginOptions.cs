namespace Ghost.Plugin.LinkedIn;

public sealed class LinkedInPluginOptions
{
    public bool UsePluginRuntime { get; init; } = true;

    public bool RegisterReadinessServices { get; init; } = true;

    public bool RegisterKeyedJobClient { get; init; } = true;
}
