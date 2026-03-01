using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Sdk.Spider.Strategies;
using Ghost.Sdk.Spider.Strategies.Contracts;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInJobClientParallelTests : ReliabilityTestBase
{
    public LinkedInJobClientParallelTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task SearchJobsParallelAsyncYieldsJobsFromGuestPages()
    {
        // Arrange: Create a mock strategy router that returns test data
        var mockRouter = new Mock<IStrategyRouter>();

        // Setup the router to return 3 test jobs when "Browser" strategy is executed
        mockRouter
            .Setup(r => r.ExecuteStrategyAsync(
                "Browser",
                It.IsAny<StrategyContext>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ExtractionResult.CreateSuccess(
                new List<JobListing>
                {
                    new() { Id = "123", Title = "Software Engineer", Company = "Tech Corp", Location = "San Francisco, CA" },
                    new() { Id = "456", Title = "Senior Developer", Company = "Innovation Labs", Location = "Remote" },
                    new() { Id = "789", Title = "DevOps Engineer", Company = "Cloud Systems", Location = "New York, NY" }
                },
                "Browser",
                TimeSpan.FromMilliseconds(100)));

        var session = new Mock<IBrowserSession>();
        IOptions<LinkedInOptions> options = Options.Create(new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.Browser });

        // Use internal constructor to inject mock router
        var client = new LinkedInJobClient(
            session.Object,
            options,
            NullLogger<LinkedInJobClient>.Instance,
            new JavaScriptAdapter(),
            new EntityParser(),
            mockRouter.Object);

        // Act
        var criteria = new JobSearchCriteria { Query = "dev", Location = "remote", MaxResults = 50 };
        int count = 0;
        await foreach (JobListing _ in client.SearchJobsParallelAsync(criteria, CancellationToken.None))
        {
            count++;
        }

        // Assert
        Assert.Equal(3, count);
    }
}
