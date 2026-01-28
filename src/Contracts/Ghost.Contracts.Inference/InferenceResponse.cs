namespace Ghost.Contracts.Inference;

/// <summary>
/// Represents a completed inference response.
/// </summary>
public sealed record InferenceResponse
{
    /// <summary>
    /// The generated content.
    /// </summary>
    public string Content { get; init; } = string.Empty;

    /// <summary>
    /// The model that produced the response.
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// The reason generation finished, as reported by the provider (eg. stop, length).
    /// </summary>
    public string? FinishReason { get; init; }

    /// <summary>
    /// Token usage information where available.
    /// </summary>
    public TokenUsage Usage { get; init; } = new TokenUsage();
}
