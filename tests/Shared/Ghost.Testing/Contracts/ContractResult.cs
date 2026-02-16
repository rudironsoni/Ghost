using System.Collections.Generic;

namespace Ghost.Testing.Contracts;

/// <summary>
/// Result of a contract test execution.
/// </summary>
public sealed record ContractResult
{
    /// <summary>
    /// Name of the contract being tested.
    /// </summary>
    public string ContractName { get; init; } = string.Empty;

    /// <summary>
    /// Whether the contract passed.
    /// </summary>
    public bool Passed { get; init; }

    /// <summary>
    /// Error messages if the contract failed.
    /// </summary>
    public IReadOnlyList<string> Errors { get; init; } = [];

    /// <summary>
    /// Additional context or diagnostic information.
    /// </summary>
    public IReadOnlyDictionary<string, object> Context { get; init; } = [];

    /// <summary>
    /// Creates a successful contract result.
    /// </summary>
    public static ContractResult Success(string contractName) =>
        new() { ContractName = contractName, Passed = true };

    /// <summary>
    /// Creates a failed contract result with errors.
    /// </summary>
    public static ContractResult Failure(string contractName, params string[] errors) =>
        new()
        {
            ContractName = contractName,
            Passed = false,
            Errors = errors
        };

    /// <summary>
    /// Creates a failed contract result with errors and context.
    /// </summary>
    public static ContractResult Failure(
        string contractName,
        IReadOnlyDictionary<string, object> context,
        params string[] errors) =>
        new()
        {
            ContractName = contractName,
            Passed = false,
            Errors = errors,
            Context = context
        };
}
