using System.Net.Http;
using Ghost;
using Ghost.Contracts.Jobs;
using Ghost.Kernel;
using Ghost.Platform.Google.Jobs;
using Ghost.Platform.Google.Tests.Contracts;
using Ghost.Testing.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Platform.Google.Tests.Contracts;

/// <summary>
/// Contract tests for Google provider.
/// </summary>
[Trait("Category", "End2End")]
public class GoogleProviderContractTests : ProviderContractTests<GoogleContractAdapter>
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="GoogleProviderContractTests"/> class.
    /// </summary>
    public GoogleProviderContractTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
    }

    /// <inheritdoc />
    protected override GoogleContractAdapter CreateAdapter()
    {
        // Add substitutes for dependencies
        var logger = Substitute.For<ILogger<Jobs.GoogleJobClient>>();
        var apiLogger = Substitute.For<ILogger<Jobs.Internal.GoogleJobsApiClient>>();

        // Add Google jobs options - use HttpOnly strategy to avoid browser dependencies
        var options = new Jobs.GoogleJobsOptions { Enabled = true, Strategy = JobSearchStrategy.HttpOnly };
        var optionsWrapper = Options.Create(options);

        // Create HTTP client
        var httpClient = new HttpClient();

        // Create Google jobs API client
        var apiClient = new Jobs.Internal.GoogleJobsApiClient(httpClient, options, apiLogger);

        // Create Google job client using API-only constructor
        var jobClient = new Jobs.GoogleJobClient(apiClient, logger, optionsWrapper);

        return new GoogleContractAdapter(jobClient);
    }
}
