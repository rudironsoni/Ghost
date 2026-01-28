namespace Ghost.Contracts.Inference;

/// <summary>
/// Represents a single message in an inference conversation.
/// </summary>
public sealed record InferenceMessage
{
    /// <summary>
    /// Role of the message author.
    /// </summary>
    public InferenceRole Role { get; init; } = InferenceRole.User;

    /// <summary>
    /// Message content.
    /// </summary>
    public string Content { get; init; } = string.Empty;
}
