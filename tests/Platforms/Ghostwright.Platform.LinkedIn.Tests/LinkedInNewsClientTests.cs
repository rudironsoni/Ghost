using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ghostwright.Platform.LinkedIn.Tests
{
    public class LinkedInNewsClientTests
    {
        [Fact]
        public async Task GetArticlesAsync_ReturnsEnumerable()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));
            mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IElement?>(null));

            var client = new LinkedInNewsClient(mockSession, new LinkedInOptions());
            var list = await client.GetArticlesAsync(CancellationToken.None);
            list.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<Article>>();
        }

        [Fact]
        public async Task SearchAsync_ReturnsEnumerable()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));
            mockPage.QuerySelectorAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult<IElement?>(null));

            var client = new LinkedInNewsClient(mockSession, new LinkedInOptions());
            var results = await client.SearchAsync("AI", CancellationToken.None);
            results.Should().BeAssignableTo<System.Collections.Generic.IEnumerable<Article>>();
        }
    }
}
