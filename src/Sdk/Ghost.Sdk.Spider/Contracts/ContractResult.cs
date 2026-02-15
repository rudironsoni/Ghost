namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Represents the result of validating a single spider contract.
/// </summary>
/// <param name="ContractName">The name of the contract that was validated.</param>
/// <param name="Passed">Whether the contract validation passed.</param>
public record ContractResult(string ContractName, bool Passed);
