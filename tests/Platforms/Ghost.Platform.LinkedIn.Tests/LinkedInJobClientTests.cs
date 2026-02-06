using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Sdk.Spider.Adapters;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInJobClientTests
{
    [Fact]
    public async Task SearchJobsAsyncReturnsEnumerable()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));
        mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(null));

        var logger = Substitute.For<ILogger<LinkedInJobClient>>();
        var jsAdapter = Substitute.For<JavaScriptAdapter>();
        var entityParser = Substitute.For<EntityParser>();
        var opts = new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.BrowserPage };
        var client = new LinkedInJobClient(mockSession, Options.Create(opts), logger, jsAdapter, entityParser);
        var jobs = await client.SearchJobsAsync(new JobSearchCriteria { Query = "developer" }, CancellationToken.None);
        jobs.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<JobListing>>();
    }

    [Fact]
    public async Task ApplyAsyncReturnsFalseWhenNoApplyButton()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));
        mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(null));

        var logger = Substitute.For<ILogger<LinkedInJobClient>>();
        var jsAdapter = Substitute.For<JavaScriptAdapter>();
        var entityParser = Substitute.For<EntityParser>();
        var opts = new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.BrowserPage };
        var client = new LinkedInJobClient(mockSession, Options.Create(opts), logger, jsAdapter, entityParser);
        var result = await client.ApplyAsync("job:1", new ApplicationDetails { ApplicantName = "Test", ApplicantEmail = "a@b.com" }, CancellationToken.None);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetJobDetailsBrowserAsyncSetsProxyTimezoneWhenConfigured()
    {
        // Arrange
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();

        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));

        var jsAdapter = Substitute.For<JavaScriptAdapter>();
        var entityParser = Substitute.For<EntityParser>();
        var opts = new LinkedInOptions
        {
            TimezoneId = "Europe/Madrid",
            Locale = "es-ES",
            ScrapingStrategy = JobScrapingStrategy.BrowserPage
        };

        var client = new LinkedInJobClient(mockSession, Options.Create(opts), null!, jsAdapter, entityParser);

        // Act
        await client.GetJobDetailsAsync("123");

        // Assert
        await mockSession.Received().NewPageAsync(Arg.Is<PageOptions>(p =>
            p.TimezoneId == "Europe/Madrid" &&
            p.Locale == "es-ES"
        ), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetJobDetailsBrowserAsyncDetectsEasyApply()
    {
        // Arrange
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();

        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));

        var mockBtn = Substitute.For<IElement>();
        mockBtn.GetTextContentAsync(Arg.Any<CancellationToken>()).Returns("Easy Apply");

        // Mock query selector to return the button
        mockPage.QuerySelectorAsync(Arg.Is<string>(s => s.Contains("jobs-apply-button") || s.Contains("jobs-s-apply")), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(mockBtn));

        var jsAdapter = Substitute.For<JavaScriptAdapter>();
        var entityParser = Substitute.For<EntityParser>();
        var opts = new LinkedInOptions { ScrapingStrategy = JobScrapingStrategy.BrowserPage };
        var client = new LinkedInJobClient(mockSession, Options.Create(opts), null!, jsAdapter, entityParser);

        // Act
        var result = await client.GetJobDetailsAsync("123");

        // Assert
        result.Should().NotBeNull();
        result.IsEasyApply.Should().BeTrue();
    }
}
