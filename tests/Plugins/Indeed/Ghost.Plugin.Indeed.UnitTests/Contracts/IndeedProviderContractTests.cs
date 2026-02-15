using Ghost.Testing.Contracts;
using Xunit;
using Xunit.Abstractions;
using Ghost.Platform.Indeed;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed.Tests.Contracts;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Ghost.Platform.Common.Session;
using Ghost.Platform.Indeed.Internal;

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
        var proxyProvider = Substitute.For<IProxyProvider>();
        var sessionOrchestrator = Substitute.For<ISessionOrchestrator>();
        var browserSession = Substitute.For<IBrowserSession>();
        var jsonLdExtractor = Substitute.For<IJsonLdExtractor>();
        var logger = Substitute.For<ILogger<IndeedJobClient>>();
        var apiLogger = Substitute.For<ILogger<IndeedApiClient>>();
        var scraperLogger = Substitute.For<ILogger<Jobs.IndeedSearchScraper>>();
        var detailsLogger = Substitute.For<ILogger<Jobs.IndeedJobDetailsScraper>>();

        // Add Indeed options
        var options = new IndeedOptions { Enabled = true, Country = Ghost.Models.CountryCode.US };

        // Create Indeed API client
        var apiClient = new IndeedApiClient(proxyProvider, sessionOrchestrator, options, apiLogger);

        // Create Indeed scrapers
        var searchScraper = new Jobs.IndeedSearchScraper(apiClient, scraperLogger, browserSession, options);
        var detailsScraper = new Jobs.IndeedJobDetailsScraper(browserSession, detailsLogger, jsonLdExtractor, options);

        // Create Indeed job client
        var jobClient = new IndeedJobClient(apiClient, logger, searchScraper, detailsScraper);

        return new IndeedContractAdapter(jobClient);
    }
}
