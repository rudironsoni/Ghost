namespace Ghost.Plugin.OpenAI;

public sealed class OpenAIPluginOptions
{
    public bool UsePluginRuntime { get; init; } = true;

    public bool RegisterReadinessServices { get; init; } = true;

    public bool RegisterKeyedInferenceClient { get; init; } = true;
}
