namespace Ghost.Sdk.Certification;

/// <summary>
/// Validates SpiderSpec schema for correctness.
/// </summary>
public interface ISchemaValidator
{
    /// <summary>
    /// Validates a SpiderSpec against the schema.
    /// </summary>
    /// <param name="spec">SpiderSpec to validate</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Validation result with any errors</returns>
    Task<SchemaValidationResult> ValidateAsync(
        Ghost.Sdk.Contracts.SpiderSpec spec,
        CancellationToken ct = default);
}

/// <summary>
/// Result of schema validation.
/// </summary>
public sealed record SchemaValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
