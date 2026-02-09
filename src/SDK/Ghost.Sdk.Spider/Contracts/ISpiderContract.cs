namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Defines a contract that validates spider behavior during execution.
/// </summary>
/// <remarks>
/// Spider contracts provide a mechanism for testing and enforcing constraints
/// on spider execution, such as maximum requests, duration limits, or minimum items extracted.
/// </remarks>
public interface ISpiderContract
{
    /// <summary>
    /// Gets the name of this contract.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Validates the spider context against this contract's rules.
    /// </summary>
    /// <param name="context">The spider context to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the contract is satisfied; otherwise, false.</returns>
    Task<bool> ValidateAsync(SpiderContext context, CancellationToken ct = default);
}
