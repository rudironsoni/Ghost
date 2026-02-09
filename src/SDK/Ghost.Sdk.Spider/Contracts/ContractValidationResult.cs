using System.Collections.Immutable;

namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Represents the aggregated results of validating multiple spider contracts.
/// </summary>
public class ContractValidationResult
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ContractValidationResult"/> class.
    /// </summary>
    /// <param name="results">The collection of individual contract validation results.</param>
    public ContractValidationResult(IEnumerable<ContractResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);
        Results = results.ToImmutableList();
    }

    /// <summary>
    /// Gets the collection of individual contract validation results.
    /// </summary>
    public ImmutableList<ContractResult> Results { get; }

    /// <summary>
    /// Gets a value indicating whether all contracts passed validation.
    /// </summary>
    public bool AllPassed => Results.All(r => r.Passed);

    /// <summary>
    /// Gets the number of contracts that passed validation.
    /// </summary>
    public int PassedCount => Results.Count(r => r.Passed);

    /// <summary>
    /// Gets the number of contracts that failed validation.
    /// </summary>
    public int FailedCount => Results.Count(r => !r.Passed);

    /// <summary>
    /// Gets the total number of contracts validated.
    /// </summary>
    public int TotalCount => Results.Count;

    /// <summary>
    /// Gets the names of all contracts that failed validation.
    /// </summary>
    public IEnumerable<string> FailedContracts => Results
        .Where(r => !r.Passed)
        .Select(r => r.ContractName);
}
