namespace Ghost.Contracts.Inference;

/// <summary>
/// Token usage counts returned by inference providers.
/// </summary>
public sealed record TokenUsage
{
    /// <summary>
    /// Number of tokens in the prompt.
    /// </summary>
    public int PromptTokens { get; init; }

    /// <summary>
    /// Number of tokens generated in the completion.
    /// </summary>
    public int CompletionTokens { get; init; }

    /// <summary>
    /// Total tokens consumed.
    /// </summary>
    public int TotalTokens { get; init; }
}
