using Ghost.Testing.Contracts;
using Xunit;
using Xunit.Abstractions;
using Ghost.Platform.InfoJobs.Jobs;
using Ghost.Contracts.Jobs;
using Ghost.Platform.InfoJobs.Tests.Contracts;
using NSubstitute;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;
using Ghost.Platform.Common.Session;
using Ghost.Platform.InfoJobs.Jobs.Internal;

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
        var sessionOrchestrator = Substitute.For<ISessionOrchestrator>();
        var logger = Substitute.For<ILogger<InfoJobClient>>();
        var apiLogger = Substitute.For<ILogger<InfoJobsApiClient>>();

        // Add InfoJobs options
        var options = new InfoJobsOptions { Enabled = true };

        // Create InfoJobs API client
        var apiClient = new InfoJobsApiClient(sessionOrchestrator, options, apiLogger);

        // Create InfoJobs job client
        var jobClient = new InfoJobClient(apiClient, logger);

        return new InfoJobsContractAdapter(jobClient);
    }
}
