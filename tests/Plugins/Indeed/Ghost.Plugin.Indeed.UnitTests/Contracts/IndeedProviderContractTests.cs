using Ghost.Contracts.Jobs;
using Ghost.Infrastructure.Session;
using Ghost.Platform.Indeed;
using Ghost.Plugin.Indeed.Internal;
using Ghost.Platform.Indeed.Tests.Contracts;
using Ghost.Testing.Contracts;
using Microsoft.Extensions.Logging;
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
        IProxyProvider proxyProvider = Substitute.For<IProxyProvider>();
        var sessionOrchestrator = Substitute.For<ISessionOrchestrator>();
        IBrowserSession browserSession = Substitute.For<IBrowserSession>();
        IJsonLdExtractor jsonLdExtractor = Substitute.For<IJsonLdExtractor>();
        ILogger<IndeedJobClient> logger = Substitute.For<ILogger<IndeedJobClient>>();
        ILogger<IndeedApiClient> apiLogger = Substitute.For<ILogger<IndeedApiClient>>();
        ILogger<Jobs.IndeedSearchScraper> scraperLogger = Substitute.For<ILogger<Jobs.IndeedSearchScraper>>();
        ILogger<Jobs.IndeedJobDetailsScraper> detailsLogger = Substitute.For<ILogger<Jobs.IndeedJobDetailsScraper>>();

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
