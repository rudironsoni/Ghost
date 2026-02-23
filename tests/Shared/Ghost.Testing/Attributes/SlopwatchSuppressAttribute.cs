namespace Ghost.Testing.Attributes;

/// <summary>
/// Attribute to suppress Slopwatch analyzer warnings in test code.
/// Use this attribute to document intentional exceptions to coding standards
/// with a clear justification.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class | AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class SlopwatchSuppressAttribute : Attribute
{
    /// <summary>
    /// Gets the rule ID being suppressed (e.g., "SW004").
    /// </summary>
    public string RuleId { get; }

    /// <summary>
    /// Gets the justification for suppressing the rule.
    /// Must be at least 20 characters long.
    /// </summary>
    public string Justification { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SlopwatchSuppressAttribute"/> class.
    /// </summary>
    /// <param name="ruleId">The rule ID to suppress (e.g., "SW004").</param>
    /// <param name="justification">The reason for suppression (minimum 20 characters).</param>
    /// <exception cref="ArgumentException">Thrown when justification is less than 20 characters.</exception>
    public SlopwatchSuppressAttribute(string ruleId, string justification)
    {
        if (string.IsNullOrEmpty(justification) || justification.Length < 20)
        {
            throw new ArgumentException(
                "Justification must be at least 20 characters long to ensure proper documentation.",
                nameof(justification));
        }

        RuleId = ruleId ?? throw new ArgumentNullException(nameof(ruleId));
        Justification = justification;
    }
}
