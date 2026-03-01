using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Testing.Contracts;
using Ghost.Testing.Contracts.BuiltIn;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Testing.Contracts;

/// <summary>
/// Base class for provider contract tests.
/// Each provider should inherit from this class and provide its adapter.
/// </summary>
/// <typeparam name="TAdapter">The provider contract adapter type.</typeparam>
public abstract class ProviderContractTests<TAdapter>
    where TAdapter : class, IProviderContractAdapter
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProviderContractTests{TAdapter}"/> class.
    /// </summary>
    protected ProviderContractTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Gets the provider adapter instance.
    /// </summary>
    protected abstract TAdapter CreateAdapter();

    /// <summary>
    /// Tests that all required fields are present in job listings.
    /// </summary>
    [Fact]
    public async Task RequiredFields_ArePresentAsync()
    {
        TAdapter adapter = CreateAdapter();
        var contract = new RequiredFieldsContract();
        ContractResult result = await contract.ExecuteAsync(adapter);

        _output.WriteLine($"Contract: {result.ContractName}");
        _output.WriteLine($"Passed: {result.Passed}");

        if (!result.Passed)
        {
            foreach (string error in result.Errors)
            {
                _output.WriteLine($"Error: {error}");
            }
        }

        Assert.True(result.Passed, $"Required fields contract failed: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// Tests that deduplication is correct.
    /// </summary>
    [Fact]
    public async Task Dedupe_IsCorrectAsync()
    {
        TAdapter adapter = CreateAdapter();
        var contract = new DedupeContract();
        ContractResult result = await contract.ExecuteAsync(adapter);

        _output.WriteLine($"Contract: {result.ContractName}");
        _output.WriteLine($"Passed: {result.Passed}");

        if (!result.Passed)
        {
            foreach (string error in result.Errors)
            {
                _output.WriteLine($"Error: {error}");
            }
        }

        Assert.True(result.Passed, $"Dedupe contract failed: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// Tests that pagination is complete and correct.
    /// </summary>
    [Fact]
    public async Task Pagination_IsCompleteAsync()
    {
        TAdapter adapter = CreateAdapter();
        var contract = new PaginationContract();
        ContractResult result = await contract.ExecuteAsync(adapter);

        _output.WriteLine($"Contract: {result.ContractName}");
        _output.WriteLine($"Passed: {result.Passed}");

        if (!result.Passed)
        {
            foreach (string error in result.Errors)
            {
                _output.WriteLine($"Error: {error}");
            }
        }

        Assert.True(result.Passed, $"Pagination contract failed: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// Tests that retry and backoff behavior is correct.
    /// </summary>
    [Fact]
    public async Task RetryBehavior_IsCorrectAsync()
    {
        TAdapter adapter = CreateAdapter();
        var contract = new RetryBehaviorContract();
        ContractResult result = await contract.ExecuteAsync(adapter);

        _output.WriteLine($"Contract: {result.ContractName}");
        _output.WriteLine($"Passed: {result.Passed}");

        if (!result.Passed)
        {
            foreach (string error in result.Errors)
            {
                _output.WriteLine($"Error: {error}");
            }
        }

        Assert.True(result.Passed, $"Retry behavior contract failed: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// Tests that consent flow is handled correctly.
    /// </summary>
    [Fact]
    public async Task ConsentFlow_IsCompliantAsync()
    {
        TAdapter adapter = CreateAdapter();
        var contract = new ConsentComplianceContract();
        ContractResult result = await contract.ExecuteAsync(adapter);

        _output.WriteLine($"Contract: {result.ContractName}");
        _output.WriteLine($"Passed: {result.Passed}");

        if (!result.Passed)
        {
            foreach (string error in result.Errors)
            {
                _output.WriteLine($"Error: {error}");
            }
        }

        Assert.True(result.Passed, $"Consent compliance contract failed: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// Tests that extraction is idempotent.
    /// </summary>
    [Fact]
    public async Task Extraction_IsIdempotentAsync()
    {
        TAdapter adapter = CreateAdapter();
        var contract = new IdempotentExtractionContract();
        ContractResult result = await contract.ExecuteAsync(adapter);

        _output.WriteLine($"Contract: {result.ContractName}");
        _output.WriteLine($"Passed: {result.Passed}");

        if (!result.Passed)
        {
            foreach (string error in result.Errors)
            {
                _output.WriteLine($"Error: {error}");
            }
        }

        Assert.True(result.Passed, $"Idempotent extraction contract failed: {string.Join(", ", result.Errors)}");
    }

    /// <summary>
    /// Runs all contracts and reports results.
    /// </summary>
    [Fact]
    public async Task AllContracts_PassAsync()
    {
        TAdapter adapter = CreateAdapter();
        var contracts = new List<IProviderContract>
        {
            new RequiredFieldsContract(),
            new DedupeContract(),
            new PaginationContract(),
            new RetryBehaviorContract(),
            new ConsentComplianceContract(),
            new IdempotentExtractionContract()
        };

        var runner = new ContractRunner(contracts);
        ContractRunResult result = await runner.RunAsync(adapter);

        _output.WriteLine($"Platform: {result.PlatformName}");
        _output.WriteLine($"Overall Passed: {result.Passed}");
        _output.WriteLine($"Total Contracts: {result.Results.Count}");
        _output.WriteLine($"Passed Contracts: {result.Results.Count(r => r.Passed)}");
        _output.WriteLine($"Failed Contracts: {result.FailedResults.Count}");

        foreach (ContractResult contractResult in result.Results)
        {
            _output.WriteLine($"  {contractResult.ContractName}: {(contractResult.Passed ? "PASS" : "FAIL")}");

            if (!contractResult.Passed)
            {
                foreach (string error in contractResult.Errors)
                {
                    _output.WriteLine($"    - {error}");
                }
            }
        }

        Assert.True(result.Passed, $"One or more contracts failed: {string.Join(", ", result.FailedResults.Select(r => r.ContractName))}");
    }
}
