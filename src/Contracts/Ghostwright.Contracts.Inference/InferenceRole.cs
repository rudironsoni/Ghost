namespace Ghostwright.Contracts.Inference;

/// <summary>
/// Message role in a conversation.
/// </summary>
public enum InferenceRole
{
    /// <summary>
    /// System-level instructions.
    /// </summary>
    System,

    /// <summary>
    /// User message.
    /// </summary>
    User,

    /// <summary>
    /// Assistant / model-generated message.
    /// </summary>
    Assistant
}
