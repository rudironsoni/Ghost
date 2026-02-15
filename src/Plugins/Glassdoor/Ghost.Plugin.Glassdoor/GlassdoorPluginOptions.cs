namespace Ghost.Plugin.Glassdoor;

public sealed class GlassdoorPluginOptions
{
    public bool UsePluginRuntime { get; init; } = true;

    public bool RegisterReadinessServices { get; init; } = true;

    public bool RegisterKeyedJobClient { get; init; } = true;
}
