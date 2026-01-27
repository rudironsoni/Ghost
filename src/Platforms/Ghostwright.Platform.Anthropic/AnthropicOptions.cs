namespace Ghostwright.Platform.Anthropic;

/// <summary>
/// Options for the Anthropic platform integration.
/// </summary>
public sealed class AnthropicOptions
{
    /// <summary>
    /// Base url for Claude web UI.
    /// </summary>
    public string BaseUrl { get; set; } = "https://claude.ai";

    /// <summary>
    /// Response timeout for generation operations.
    /// </summary>
    public TimeSpan ResponseTimeout { get; set; } = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Default model identifier.
    /// </summary>
    public string DefaultModel { get; set; } = "claude-3-opus";
}
