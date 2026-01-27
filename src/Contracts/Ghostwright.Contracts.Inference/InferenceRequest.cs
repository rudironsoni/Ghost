using System.Collections.Generic;

namespace Ghostwright.Contracts.Inference;

/// <summary>
/// Represents a request to an inference provider.
/// </summary>
public sealed record InferenceRequest
{
    /// <summary>
    /// The model name to use.
    /// </summary>
    public string Model { get; init; } = string.Empty;

    /// <summary>
    /// A sequence of messages composing the conversation or prompt.
    /// </summary>
    public IReadOnlyList<InferenceMessage> Messages { get; init; } = System.Array.Empty<InferenceMessage>();

    /// <summary>
    /// Sampling temperature. 0.0 = deterministic. Defaults to 0.0.
    /// </summary>
    public double Temperature { get; init; }

    /// <summary>
    /// Maximum number of tokens to generate. Zero means provider default.
    /// </summary>
    public int MaxTokens { get; init; }

    /// <summary>
    /// Top-p nucleus sampling. Defaults to 1.0 (disabled).
    /// </summary>
    public double TopP { get; init; } = 1.0;

    /// <summary>
    /// Optional stop sequences to halt generation.
    /// </summary>
    public IReadOnlyList<string> StopSequences { get; init; } = System.Array.Empty<string>();

    /// <summary>
    /// Optional system prompt to guide the assistant's behavior.
    /// </summary>
    public string? SystemPrompt { get; init; }
}
