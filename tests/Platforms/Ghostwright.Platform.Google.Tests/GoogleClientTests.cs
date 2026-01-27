using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ghostwright.Platform.Google.Tests
{
    public class GoogleClientTests
    {
        [Fact]
        public async Task CompleteAsync_ReturnsText_WhenPageEvaluates()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));

            mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("ok"));

            var client = new GoogleClient(mockSession, new GoogleOptions());
            var resp = await client.CompleteAsync("p", CancellationToken.None);
            resp.Text.Should().Be("ok");
        }

        [Fact]
        public async Task StreamAsync_InvokesHandler()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));
            mockPage.EvaluateAsync(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var client = new GoogleClient(mockSession, new GoogleOptions());
            var called = false;
            await client.StreamAsync("p", chunk => { called = true; return Task.CompletedTask; }, CancellationToken.None);
            called.Should().BeTrue();
        }
    }
}
