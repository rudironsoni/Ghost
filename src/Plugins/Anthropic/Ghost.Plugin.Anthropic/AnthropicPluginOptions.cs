namespace Ghost.Plugin.Anthropic;

public sealed class AnthropicPluginOptions
{
    public bool UsePluginRuntime { get; init; } = true;

    public bool RegisterReadinessServices { get; init; } = true;

    public bool RegisterKeyedInferenceClient { get; init; } = true;
}
