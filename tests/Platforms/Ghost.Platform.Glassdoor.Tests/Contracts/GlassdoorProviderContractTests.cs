using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Ghost.Platform.Glassdoor;
using Ghost.Platform.Glassdoor.Internal;
using Ghost.Platform.Glassdoor.Tests.Contracts;
using Ghost.Testing.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

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
        var kernel = Substitute.For<GhostKernel>();
        var logger = Substitute.For<ILogger<GlassdoorJobClient>>();
        var apiLogger = Substitute.For<ILogger<GlassdoorApiClient>>();
        var browserClientLogger = Substitute.For<ILogger<GlassdoorBrowserClient>>();
        var scraperLogger = Substitute.For<ILogger<Jobs.GlassdoorSearchScraper>>();

        // Add Glassdoor options
        var options = new GlassdoorOptions { Enabled = true };
        var optionsWrapper = Options.Create(options);

        // Create HTTP client
        var httpClient = new HttpClient();

        // Create Glassdoor API client
        var apiClient = new GlassdoorApiClient(httpClient, apiLogger);

        // Create Glassdoor browser client
        var browserClient = new GlassdoorBrowserClient(kernel, optionsWrapper, browserClientLogger);

        // Create Glassdoor search scraper
        var searchScraper = new Jobs.GlassdoorSearchScraper(kernel, optionsWrapper, scraperLogger);

        // Create Glassdoor job client
        var jobClient = new GlassdoorJobClient(apiClient, browserClient, searchScraper, optionsWrapper, logger);

        return new GlassdoorContractAdapter(jobClient);
    }
}
