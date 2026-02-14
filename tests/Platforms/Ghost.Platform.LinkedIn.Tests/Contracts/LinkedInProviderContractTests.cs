using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Common.Session;
using Ghost.Platform.LinkedIn;
using Ghost.Plugin.LinkedIn.Tests.Contracts;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Testing.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.LinkedIn.Tests.Contracts;

/// <summary>
/// Contract tests for LinkedIn provider.
/// </summary>
[Trait("Category", "E2E")]
public class LinkedInProviderContractTests : ProviderContractTests<LinkedInContractAdapter>
{
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Initializes a new instance of the <see cref="LinkedInProviderContractTests"/> class.
    /// </summary>
    public LinkedInProviderContractTests(ITestOutputHelper output) : base(output)
    {
        _output = output;
    }

    /// <inheritdoc />
    protected override LinkedInContractAdapter CreateAdapter()
    {
        // Add substitutes for dependencies
        var browserSession = Substitute.For<IBrowserSession>();
        var sessionOrchestrator = Substitute.For<ISessionOrchestrator>();
        var logger = Substitute.For<ILogger<LinkedInJobClient>>();

        // Add LinkedIn options
        var options = new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.BrowserPage };

        // Create LinkedIn job client
        var jobClient = new LinkedInJobClient(browserSession, Options.Create(options), logger, new JavaScriptAdapter(), new EntityParser());

        return new LinkedInContractAdapter(jobClient);
    }
}
