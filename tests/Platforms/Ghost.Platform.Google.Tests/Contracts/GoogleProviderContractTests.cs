using System.Net.Http;
using Ghost;
using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Core;
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
        var browserSession = Substitute.For<IBrowserSession>();
        var logger = Substitute.For<ILogger<Jobs.GoogleJobClient>>();
        var apiLogger = Substitute.For<ILogger<Jobs.Internal.GoogleJobsApiClient>>();
        var browserLogger = Substitute.For<ILogger<Jobs.Internal.GoogleJobsBrowserClient>>();
        var scraperLogger = Substitute.For<ILogger<Jobs.Internal.GoogleJobsScraper>>();
        var kernel = Substitute.For<GhostKernel>();

        // Add Google jobs options
        var options = new Jobs.GoogleJobsOptions { Enabled = true };
        var optionsWrapper = Options.Create(options);

        // Create HTTP client
        var httpClient = new HttpClient();

        // Create Google jobs API client
        var apiClient = new Jobs.Internal.GoogleJobsApiClient(httpClient, options, apiLogger);

        // Create Google jobs browser client
        var browserClient = new Jobs.Internal.GoogleJobsBrowserClient(kernel, optionsWrapper, browserLogger);

        // Create Google jobs scraper
        var scraper = new Jobs.Internal.GoogleJobsScraper(httpClient, scraperLogger);

        // Create Google job client
        var jobClient = new Jobs.GoogleJobClient(apiClient, browserClient, scraper, logger, optionsWrapper);

        return new GoogleContractAdapter(jobClient);
    }
}
