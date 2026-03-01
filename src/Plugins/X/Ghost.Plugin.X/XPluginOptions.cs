namespace Ghost.Plugin.X;

public sealed class XPluginOptions
{
    public bool UsePluginRuntime { get; init; } = true;

    public bool RegisterReadinessServices { get; init; } = true;

    public bool RegisterKeyedSocialClient { get; init; } = true;
}
