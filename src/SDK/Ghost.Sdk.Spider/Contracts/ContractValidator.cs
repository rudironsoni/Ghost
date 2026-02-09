namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Orchestrates the validation of multiple spider contracts.
/// </summary>
public class ContractValidator
{
    private readonly List<ISpiderContract> _contracts = [];

    /// <summary>
    /// Adds a contract to the validator.
    /// </summary>
    /// <param name="contract">The contract to add.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="contract"/> is null.</exception>
    public void AddContract(ISpiderContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        _contracts.Add(contract);
    }

    /// <summary>
    /// Gets the count of registered contracts.
    /// </summary>
    public int ContractCount => _contracts.Count;

    /// <summary>
    /// Validates all registered contracts against the provided spider context.
    /// </summary>
    /// <param name="context">The spider context to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A <see cref="ContractValidationResult"/> containing the results of all contract validations.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    public async Task<ContractValidationResult> ValidateAllAsync(SpiderContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var results = new List<ContractResult>();
        foreach (var contract in _contracts)
        {
            var passed = await contract.ValidateAsync(context, ct);
            results.Add(new ContractResult(contract.Name, passed));
        }

        return new ContractValidationResult(results);
    }
}
