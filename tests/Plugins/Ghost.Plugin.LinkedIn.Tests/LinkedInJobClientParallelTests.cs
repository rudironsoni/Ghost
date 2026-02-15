using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInJobClientParallelTests
{
    [Fact]
    public async Task SearchJobsParallelAsyncYieldsJobsFromGuestPages()
    {
        var session = new Mock<IBrowserSession>();
        var searchPage = new Mock<IPage>();

        // Mock job nodes (3 jobs total across search results)
        Mock<IElement> job1 = CreateMockJobNode("123", "Software Engineer", "Tech Corp", "San Francisco, CA");
        Mock<IElement> job2 = CreateMockJobNode("456", "Senior Developer", "Innovation Labs", "Remote");
        Mock<IElement> job3 = CreateMockJobNode("789", "DevOps Engineer", "Cloud Systems", "New York, NY");

        IElement[] jobNodes = new[] { job1.Object, job2.Object, job3.Object };

        // Setup search page to return job nodes
        searchPage.Setup(p => p.QuerySelectorAllAsync(
            It.Is<string>(s => s.Contains(".jobs-search-results__list-item") || s.Contains(".base-card")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(jobNodes);

        searchPage.Setup(p => p.EvaluateAsync<string>("document.title", It.IsAny<CancellationToken>()))
            .ReturnsAsync("LinkedIn Jobs");

        searchPage.Setup(p => p.EvaluateAsync<object>(It.Is<string>(s => s.Contains("document.cookie")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new object());

        // Mock detail pages for each job (returned by GetJobDetailsAsync)
        Mock<IPage> detailPage1 = CreateMockDetailPage("123");
        Mock<IPage> detailPage2 = CreateMockDetailPage("456");
        Mock<IPage> detailPage3 = CreateMockDetailPage("789");

        session.SetupSequence(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(searchPage.Object)
            .ReturnsAsync(detailPage1.Object)
            .ReturnsAsync(detailPage2.Object)
            .ReturnsAsync(detailPage3.Object);

        IOptions<LinkedInOptions> options = Options.Create(new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.Browser });
        var client = new LinkedInJobClient(session.Object, options, NullLogger<LinkedInJobClient>.Instance, new JavaScriptAdapter(), new EntityParser());

        var criteria = new JobSearchCriteria { Query = "dev", Location = "remote", MaxResults = 50 };
        int count = 0;
        await foreach (JobListing _ in client.SearchJobsParallelAsync(criteria, CancellationToken.None).ConfigureAwait(false))
        {
            count++;
        }

        Assert.Equal(3, count);
    }

    private static Mock<IElement> CreateMockJobNode(string jobId, string title, string company, string location)
    {
        var node = new Mock<IElement>();

        // Mock data-entity-urn element
        var urnElement = new Mock<IElement>();
        urnElement.Setup(e => e.GetAttributeAsync("data-entity-urn", It.IsAny<CancellationToken>()))
            .ReturnsAsync($"urn:li:jobPosting:{jobId}");
        urnElement.Setup(e => e.GetAttributeAsync("data-id", It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        node.Setup(n => n.QuerySelectorAsync(
            It.Is<string>(s => s.Contains("[data-id]") || s.Contains("[data-entity-urn]")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(urnElement.Object);

        // Mock title element
        var titleElement = new Mock<IElement>();
        titleElement.Setup(e => e.GetTextContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(title);

        node.Setup(n => n.QuerySelectorAsync(
            It.Is<string>(s => s.Contains(".job-card-list__title") || s.Contains(".base-search-card__title")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(titleElement.Object);

        // Mock company element
        var companyElement = new Mock<IElement>();
        companyElement.Setup(e => e.GetTextContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(company);

        node.Setup(n => n.QuerySelectorAsync(
            It.Is<string>(s => s.Contains(".job-card-container__company-name") || s.Contains(".base-search-card__subtitle")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(companyElement.Object);

        // Mock location element
        var locationElement = new Mock<IElement>();
        locationElement.Setup(e => e.GetTextContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(location);

        node.Setup(n => n.QuerySelectorAsync(
            It.Is<string>(s => s.Contains(".job-card-container__metadata-item") || s.Contains(".job-search-card__location")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(locationElement.Object);

        // Mock link element
        var linkElement = new Mock<IElement>();
        linkElement.Setup(e => e.GetAttributeAsync("href", It.IsAny<CancellationToken>()))
            .ReturnsAsync($"https://www.linkedin.com/jobs/view/{jobId}");

        node.Setup(n => n.QuerySelectorAsync(
            It.Is<string>(s => s.Contains("a.base-card__full-link") || s.Contains("a.job-card-list__title")),
            It.IsAny<CancellationToken>()))
            .ReturnsAsync(linkElement.Object);

        return node;
    }

    private static Mock<IPage> CreateMockDetailPage(string jobId)
    {
        var page = new Mock<IPage>();

        // Mock GetContentAsync to return minimal HTML for EntityParser
        page.Setup(p => p.GetContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync($"<html><body><h1>Job {jobId}</h1></body></html>");

        return page;
    }
}
