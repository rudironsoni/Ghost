namespace Ghost.Plugin.OpenAI;

/// <summary>
/// Options for the OpenAI (chatgpt.com) browser integration.
/// </summary>
public sealed class OpenAIOptions
{
    public string BaseUrl { get; set; } = "https://chatgpt.com";
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromMinutes(2);
    public string DefaultModel { get; set; } = "gpt-4o";
}
