namespace Ghost.Plugin.Google;

public sealed class GooglePluginOptions
{
    public bool UsePluginRuntime { get; init; } = true;

    public bool RegisterReadinessServices { get; init; } = true;

    public bool RegisterKeyedInferenceClient { get; init; } = true;
}
