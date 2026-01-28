using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Ghost.Contracts.News;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInNewsClientTests
{
    [Fact]
    public async Task GetArticlesAsync_ReturnsEnumerable()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));
        mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(null));

        var logger = Substitute.For<ILogger<LinkedInNewsClient>>();
        var client = new LinkedInNewsClient(mockSession, Options.Create(new LinkedInOptions()), logger);
        var list = await client.GetArticlesAsync(null, CancellationToken.None);
        list.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<NewsArticle>>();
    }

    [Fact]
    public async Task SearchAsync_ReturnsEnumerable()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));
        mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IElement?>(null));

        var logger = Substitute.For<ILogger<LinkedInNewsClient>>();
        var client = new LinkedInNewsClient(mockSession, Options.Create(new LinkedInOptions()), logger);
        var results = await client.SearchAsync("AI", new NewsSearchOptions { MaxResults = 10 }, CancellationToken.None);
        results.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<NewsArticle>>();
    }
}
