namespace Ghost.Plugin.Common;

/// <summary>
/// Base class for plugin options with validation support.
/// </summary>
public abstract class PluginOptionsBase
{
    /// <summary>
    /// Validates the options.
    /// </summary>
    /// <returns>Validation result.</returns>
    public abstract ValidationResult Validate();
}

/// <summary>
/// Result of a validation operation.
/// </summary>
public sealed record ValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static ValidationResult Success() => new(true, Array.Empty<string>());

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static ValidationResult Failure(params string[] errors) => new(false, errors);
}
