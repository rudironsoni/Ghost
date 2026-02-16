using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Testing.Contracts;

/// <summary>
/// Interface for provider contracts that validate behavioral invariants.
/// </summary>
public interface IProviderContract
{
    /// <summary>
    /// Name of the contract.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Executes the contract test against the provided adapter.
    /// </summary>
    public Task<ContractResult> ExecuteAsync(
        IProviderContractAdapter adapter,
        CancellationToken ct = default);
}

/// <summary>
/// Base class for provider contracts with common functionality.
/// </summary>
public abstract class ProviderContractBase : IProviderContract
{
    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract Task<ContractResult> ExecuteAsync(
        IProviderContractAdapter adapter,
        CancellationToken ct = default);

    /// <summary>
    /// Creates a successful result.
    /// </summary>
    protected ContractResult Success() =>
        ContractResult.Success(Name);

    /// <summary>
    /// Creates a failed result with errors.
    /// </summary>
    protected ContractResult Failure(params string[] errors) =>
        ContractResult.Failure(Name, errors);

    /// <summary>
    /// Creates a failed result with errors and context.
    /// </summary>
    protected ContractResult Failure(
        IReadOnlyDictionary<string, object> context,
        params string[] errors) =>
        ContractResult.Failure(Name, context, errors);
}

/// <summary>
/// Runner for executing provider contracts.
/// </summary>
public sealed class ContractRunner
{
    private readonly IReadOnlyList<IProviderContract> _contracts;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContractRunner"/> class.
    /// </summary>
    public ContractRunner(IReadOnlyList<IProviderContract> contracts)
    {
        _contracts = contracts;
    }

    /// <summary>
    /// Runs all contracts against the provided adapter.
    /// </summary>
    public async Task<ContractRunResult> RunAsync(
        IProviderContractAdapter adapter,
        CancellationToken ct = default)
    {
        List<ContractResult> results = [];

        foreach (IProviderContract contract in _contracts)
        {
            ContractResult result = await contract.ExecuteAsync(adapter, ct).ConfigureAwait(false);
            results.Add(result);
        }

        return new ContractRunResult
        {
            PlatformName = adapter.PlatformName,
            Results = results,
            Passed = results.All(r => r.Passed)
        };
    }
}

/// <summary>
/// Result of running all contracts for a provider.
/// </summary>
public sealed record ContractRunResult
{
    /// <summary>
    /// Platform name.
    /// </summary>
    public string PlatformName { get; init; } = string.Empty;

    /// <summary>
    /// Individual contract results.
    /// </summary>
    public IReadOnlyList<ContractResult> Results { get; init; } = [];

    /// <summary>
    /// Whether all contracts passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Failed contract results.
    /// </summary>
    public IReadOnlyList<ContractResult> FailedResults =>
        Results.Where(r => !r.Passed).ToList();
}
