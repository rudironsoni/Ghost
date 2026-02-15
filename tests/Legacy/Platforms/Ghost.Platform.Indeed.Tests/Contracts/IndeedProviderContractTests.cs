using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed;
using Ghost.Platform.Indeed.Internal;
using Ghost.Platform.Indeed.Tests.Contracts;
using Ghost.Testing.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Platform.Indeed.Tests.Contracts;

/// <summary>
/// Contract tests for Indeed provider.
/// </summary>
public class IndeedProviderContractTests : ProviderContractTests<IndeedContractAdapter>
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndeedProviderContractTests"/> class.
    /// </summary>
    public IndeedProviderContractTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
    }

    /// <inheritdoc />
    protected override IndeedContractAdapter CreateAdapter()
    {
        // Add substitutes for dependencies
        var logger = Substitute.For<ILogger<IndeedJobClient>>();
        var apiLogger = Substitute.For<ILogger<IndeedApiClient>>();
        var proxyProvider = Substitute.For<IProxyProvider>();

        // Add Indeed options
        var options = new IndeedOptions { Enabled = true };
        var optionsWrapper = Options.Create(options);

        // Create Indeed API client
        var apiClient = new IndeedApiClient(proxyProvider, options, apiLogger);

        // Create Indeed job client
        var jobClient = new IndeedJobClient(apiClient, logger);

        return new IndeedContractAdapter(jobClient);
    }
}
