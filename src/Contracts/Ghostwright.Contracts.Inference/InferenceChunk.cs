namespace Ghostwright.Contracts.Inference;

/// <summary>
/// Represents a chunk of streamed inference output.
/// </summary>
public sealed record InferenceChunk
{
    /// <summary>
    /// Delta content produced in this chunk.
    /// </summary>
    public string Delta { get; init; } = string.Empty;

    /// <summary>
    /// Optional finish reason if this chunk indicates completion.
    /// </summary>
    public string? FinishReason { get; init; }
}
