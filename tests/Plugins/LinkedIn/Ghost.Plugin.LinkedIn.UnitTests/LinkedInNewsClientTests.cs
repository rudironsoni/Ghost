using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.News;
using Ghost.Testing.Reliability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Plugin.LinkedIn.Tests;

public class LinkedInNewsClientTests : ReliabilityTestBase
{
    public LinkedInNewsClientTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public async Task GetArticlesAsyncReturnsEnumerable()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);
        mockPage.Setup(p => p.QuerySelectorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IElement?)null);

        var logger = new Mock<ILogger<LinkedInNewsClient>>();
        var client = new LinkedInNewsClient(mockSession.Object, Options.Create(new LinkedInOptions()), logger.Object);
        IReadOnlyList<NewsArticle> list = await client.GetArticlesAsync(null, CancellationToken.None);
        list.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<NewsArticle>>();
    }

    [Fact]
    public async Task SearchAsyncReturnsEnumerable()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);
        mockPage.Setup(p => p.QuerySelectorAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IElement?)null);

        var logger = new Mock<ILogger<LinkedInNewsClient>>();
        var client = new LinkedInNewsClient(mockSession.Object, Options.Create(new LinkedInOptions()), logger.Object);
        IReadOnlyList<NewsArticle> results = await client.SearchAsync("AI", new NewsSearchOptions { MaxResults = 10 }, CancellationToken.None);
        results.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<NewsArticle>>();
    }
}
