using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Platform.LinkedIn.Internal;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInJobClientParallelTests
{
    [Fact]
    public async Task SearchJobsParallelAsyncYieldsJobsFromGuestPages()
    {
        var session = new Mock<IBrowserSession>();
        var firstPage = new Mock<IPage>();
        var secondPage = new Mock<IPage>();

        session.SetupSequence(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(firstPage.Object)
            .ReturnsAsync(secondPage.Object);

        firstPage.Setup(p => p.GetContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildHtml("123", "456", 50));
        secondPage.Setup(p => p.GetContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildHtml("789", null, 50));

        var options = Options.Create(new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.GuestApi });
        var client = new LinkedInJobClient(session.Object, options, NullLogger<LinkedInJobClient>.Instance, new JavaScriptAdapter(), new EntityParser());

        var criteria = new JobSearchCriteria { Query = "dev", Location = "remote", MaxResults = 50 };
        var count = 0;
        await foreach (var _ in client.SearchJobsParallelAsync(criteria, CancellationToken.None))
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    private static string BuildHtml(string firstId, string? secondId, int total)
    {
        var ids = new List<string> { firstId };
        if (!string.IsNullOrEmpty(secondId))
        {
            ids.Add(secondId);
        }

        var items = string.Empty;
        foreach (var id in ids)
        {
            items += $"<div data-entity-urn=\"urn:li:jobPosting:{id}\"></div>";
        }

        return $"<span class=\"results-context-header__job-count\">{total}</span>{items}";
    }

    private sealed class StubGuestJobSearch : IGuestJobSearch
    {
        public Task<IReadOnlyList<string>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct)
        {
            return Task.FromResult((IReadOnlyList<string>)new List<string>());
        }

        public Task<JobListing?> FetchJobDetailsAsync(string jobId, CancellationToken ct)
        {
            return Task.FromResult<JobListing?>(new JobListing { Id = jobId, Title = "Title" });
        }
    }
}
