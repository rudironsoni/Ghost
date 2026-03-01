using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInJobClientTests : ReliabilityTestBase
{
    public LinkedInJobClientTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task SearchJobsAsyncReturnsEnumerable()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);
        mockPage.Setup(p => p.QuerySelectorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IElement?)null);

        var logger = new Mock<ILogger<LinkedInJobClient>>();
        var opts = new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.BrowserPage };
        var client = new LinkedInJobClient(mockSession.Object, Options.Create(opts), logger.Object, new JavaScriptAdapter(), new EntityParser());
        IReadOnlyList<JobListing> jobs = await client.SearchJobsAsync(new JobSearchCriteria { Query = "developer" }, CancellationToken.None);
        jobs.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<JobListing>>();
    }

    [Fact]
    public async Task ApplyAsyncReturnsFalseWhenNoApplyButton()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);

        // Mock QuerySelectorAllAsync to return empty list instead of null
        mockPage.Setup(p => p.QuerySelectorAllAsync("button", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IElement>());

        mockPage.Setup(p => p.NavigateAsync(It.IsAny<string>(), It.IsAny<NavigationOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockPage.Setup(p => p.WaitForLoadStateAsync(It.IsAny<WaitOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var logger = new Mock<ILogger<LinkedInJobClient>>();
        var opts = new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.BrowserPage };
        var client = new LinkedInJobClient(mockSession.Object, Options.Create(opts), logger.Object, new JavaScriptAdapter(), new EntityParser());
        JobApplication result = await client.ApplyAsync("job:1", new ApplicationDetails { ApplicantName = "Test", ApplicantEmail = "a@b.com" }, CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetJobDetailsBrowserAsyncSetsProxyTimezoneWhenConfigured()
    {
        // Arrange
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();

        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);

        var opts = new LinkedInOptions
        {
            TimezoneId = "Europe/Madrid",
            Locale = "es-ES",
            ScrapingStrategy = JobScrapingStrategy.BrowserPage
        };

        var client = new LinkedInJobClient(mockSession.Object, Options.Create(opts), NullLogger<LinkedInJobClient>.Instance, new JavaScriptAdapter(), new EntityParser());

        // Act
        await client.GetJobDetailsAsync("123");

        // Assert
        mockSession.Verify(s => s.NewPageAsync(It.Is<PageOptions>(p =>
            p.TimezoneId == "Europe/Madrid" &&
            p.Locale == "es-ES"
        ), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetJobDetailsBrowserAsyncDetectsEasyApply()
    {
        // Arrange
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();

        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);

        mockPage.Setup(p => p.NavigateAsync(It.IsAny<string>(), It.IsAny<NavigationOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        mockPage.Setup(p => p.WaitForLoadStateAsync(It.IsAny<WaitOptions>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Mock HTML content with Easy Apply button for EntityParser
        string htmlContent = @"
            <html>
            <body>
                <div class='top-card-layout__title'>Software Engineer</div>
                <div class='topcard__org-name-link'>Test Company</div>
                <div class='topcard__flavor--bullet'>San Francisco, CA</div>
                <div class='show-more-less-html__markup'>Job description here</div>
                <div class='jobs-apply-button--top-card'>
                    <button>Easy Apply</button>
                </div>
                <link rel='canonical' href='https://www.linkedin.com/jobs/view/123' />
            </body>
            </html>";

        mockPage.Setup(p => p.GetContentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(htmlContent);

        var opts = new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.BrowserPage };
        var client = new LinkedInJobClient(mockSession.Object, Options.Create(opts), NullLogger<LinkedInJobClient>.Instance, new JavaScriptAdapter(), new EntityParser());

        // Act
        JobListing result = await client.GetJobDetailsAsync("123");

        // Assert
        result.Should().NotBeNull();
        result.IsEasyApply.Should().BeTrue();
    }
}
