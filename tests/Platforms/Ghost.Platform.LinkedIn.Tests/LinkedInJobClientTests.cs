using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInJobClientTests
{
    [Fact]
    public async Task SearchJobsAsync_ReturnsEnumerable()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(mockPage));
        mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(null));

        var logger = Substitute.For<ILogger<LinkedInJobClient>>();
        var client = new LinkedInJobClient(mockSession, Options.Create(new LinkedInOptions()), logger);
        var jobs = await client.SearchJobsAsync(new JobSearchCriteria { Query = "developer" }, CancellationToken.None);
        jobs.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<JobListing>>();
    }

    [Fact]
    public async Task ApplyAsync_ReturnsFalse_WhenNoApplyButton()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(mockPage));
        mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(null));

        var logger = Substitute.For<ILogger<LinkedInJobClient>>();
        var client = new LinkedInJobClient(mockSession, Options.Create(new LinkedInOptions()), logger);
        var result = await client.ApplyAsync("job:1", new ApplicationDetails { ApplicantName = "Test", ApplicantEmail = "a@b.com" }, CancellationToken.None);
        result.Should().BeNull();
    }
}
