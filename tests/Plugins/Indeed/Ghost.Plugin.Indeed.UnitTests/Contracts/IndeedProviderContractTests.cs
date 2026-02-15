using Ghost.Contracts.Jobs;
using Ghost.Infrastructure.Session;
using Ghost.Plugin.Indeed;
using Ghost.Plugin.Indeed.Internal;
using Ghost.Plugin.Indeed.Tests.Contracts;
using Ghost.Testing.Contracts;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.Indeed.Tests.Contracts;

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
        IProxyProvider proxyProvider = Substitute.For<IProxyProvider>();
        ISessionOrchestrator sessionOrchestrator = Substitute.For<ISessionOrchestrator>();
        IBrowserSession browserSession = Substitute.For<IBrowserSession>();
        IJsonLdExtractor jsonLdExtractor = Substitute.For<IJsonLdExtractor>();
        ILogger<IndeedJobClient> logger = Substitute.For<ILogger<IndeedJobClient>>();
        ILogger<IndeedApiClient> apiLogger = Substitute.For<ILogger<IndeedApiClient>>();
        ILogger<Ghost.Plugin.Indeed.Jobs.IndeedSearchScraper> scraperLogger = Substitute.For<ILogger<Ghost.Plugin.Indeed.Jobs.IndeedSearchScraper>>();
        ILogger<Ghost.Plugin.Indeed.Jobs.IndeedJobDetailsScraper> detailsLogger = Substitute.For<ILogger<Ghost.Plugin.Indeed.Jobs.IndeedJobDetailsScraper>>();

        // Add Indeed options
        var options = new IndeedOptions { Enabled = true, Country = Ghost.Models.CountryCode.US };

        // Create Indeed API client
        var apiClient = new IndeedApiClient(proxyProvider, sessionOrchestrator, options, apiLogger);

        // Create Indeed scrapers
        var searchScraper = new Ghost.Plugin.Indeed.Jobs.IndeedSearchScraper(apiClient, scraperLogger, browserSession, options);
        var detailsScraper = new Ghost.Plugin.Indeed.Jobs.IndeedJobDetailsScraper(browserSession, detailsLogger, jsonLdExtractor, options);

        // Create Indeed job client
        var jobClient = new IndeedJobClient(apiClient, logger, searchScraper, detailsScraper);

        return new IndeedContractAdapter(jobClient);
    }
}
