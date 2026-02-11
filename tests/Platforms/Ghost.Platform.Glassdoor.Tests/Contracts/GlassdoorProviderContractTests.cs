using Ghost.Testing.Contracts;
using Xunit;
using Xunit.Abstractions;
using Ghost.Platform.Glassdoor;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Glassdoor.Tests.Contracts;
using NSubstitute;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;
using Ghost.Platform.Common.Session;
using Ghost.Platform.Glassdoor.Internal;

namespace Ghost.Platform.Glassdoor.Tests.Contracts;

/// <summary>
/// Contract tests for Glassdoor provider.
/// </summary>
public class GlassdoorProviderContractTests : ProviderContractTests<GlassdoorContractAdapter>
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlassdoorProviderContractTests"/> class.
    /// </summary>
    public GlassdoorProviderContractTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
    }

    /// <inheritdoc />
    protected override GlassdoorContractAdapter CreateAdapter()
    {
        // Add substitutes for dependencies
        var browserSession = Substitute.For<IBrowserSession>();
        var sessionOrchestrator = Substitute.For<ISessionOrchestrator>();
        var logger = Substitute.For<ILogger<GlassdoorJobClient>>();
        var apiLogger = Substitute.For<ILogger<GlassdoorApiClient>>();
        var scraperLogger = Substitute.For<ILogger<Jobs.GlassdoorSearchScraper>>();

        // Add Glassdoor options
        var options = new GlassdoorOptions { Enabled = true };

        // Create Glassdoor API client
        var apiClient = new GlassdoorApiClient(sessionOrchestrator, options, apiLogger);

        // Create Glassdoor browser client
        var browserClient = new GlassdoorBrowserClient();

        // Create Glassdoor search scraper
        var searchScraper = new Jobs.GlassdoorSearchScraper(apiClient, scraperLogger, browserSession, options);

        // Create Glassdoor job client
        var jobClient = new GlassdoorJobClient(apiClient, logger, searchScraper);

        return new GlassdoorContractAdapter(jobClient);
    }
}
