using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInJobClientTests
{
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
        var jobs = await client.SearchJobsAsync(new JobSearchCriteria { Query = "developer" }, CancellationToken.None);
        jobs.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<JobListing>>();
    }

    [Fact]
    public async Task ApplyAsyncReturnsFalseWhenNoApplyButton()
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
        var result = await client.ApplyAsync("job:1", new ApplicationDetails { ApplicantName = "Test", ApplicantEmail = "a@b.com" }, CancellationToken.None);
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

        var client = new LinkedInJobClient(mockSession.Object, Options.Create(opts), null!, new JavaScriptAdapter(), new EntityParser());

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

        var mockBtn = new Mock<IElement>();
        mockBtn.Setup(b => b.GetTextContentAsync(It.IsAny<CancellationToken>())).ReturnsAsync("Easy Apply");

        // Mock query selector to return the button
        mockPage.Setup(p => p.QuerySelectorAsync(It.Is<string>(s => s.Contains("jobs-apply-button") || s.Contains("jobs-s-apply")), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockBtn.Object);

        var opts = new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.BrowserPage };
        var client = new LinkedInJobClient(mockSession.Object, Options.Create(opts), null!, new JavaScriptAdapter(), new EntityParser());

        // Act
        var result = await client.GetJobDetailsAsync("123");

        // Assert
        result.Should().NotBeNull();
        result.IsEasyApply.Should().BeTrue();
    }
}
