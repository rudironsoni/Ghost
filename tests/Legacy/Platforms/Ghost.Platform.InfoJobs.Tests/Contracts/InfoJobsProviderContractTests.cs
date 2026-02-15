using Ghost.Contracts.Jobs;
using Ghost.Platform.InfoJobs.Jobs;
using Ghost.Platform.InfoJobs.Jobs.Internal;
using Ghost.Platform.InfoJobs.Tests.Contracts;
using Ghost.Testing.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Platform.InfoJobs.Tests.Contracts;

/// <summary>
/// Contract tests for InfoJobs provider.
/// </summary>
public class InfoJobsProviderContractTests : ProviderContractTests<InfoJobsContractAdapter>
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="InfoJobsProviderContractTests"/> class.
    /// </summary>
    public InfoJobsProviderContractTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
    }

    /// <inheritdoc />
    protected override InfoJobsContractAdapter CreateAdapter()
    {
        // Add substitutes for dependencies
        var logger = Substitute.For<ILogger<InfoJobClient>>();
        var apiLogger = Substitute.For<ILogger<InfoJobsApiClient>>();

        // Add InfoJobs options
        var options = new InfoJobsOptions { Enabled = true };
        var optionsWrapper = Options.Create(options);

        // Create HTTP client
        var httpClient = new HttpClient();

        // Create InfoJobs API client
        var apiClient = new InfoJobsApiClient(httpClient, options, apiLogger);

        // Create InfoJobs job client
        var jobClient = new InfoJobClient(apiClient, logger);

        return new InfoJobsContractAdapter(jobClient);
    }
}
