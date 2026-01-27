using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ghostwright.Platform.OpenAI.Tests
{
    public class OpenAIClientTests
    {
        [Fact]
        public async Task CompleteAsync_ReturnsText_WhenPageEvaluates()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));

            mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("response text"));

            var client = new OpenAIClient(mockSession, new OpenAIOptions());
            var resp = await client.CompleteAsync("prompt", CancellationToken.None);
            resp.Text.Should().Be("response text");
        }

        [Fact]
        public async Task StreamAsync_CallsHandler()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));

            mockPage.EvaluateAsync(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask);

            var client = new OpenAIClient(mockSession, new OpenAIOptions());
            var invoked = false;
            await client.StreamAsync("prompt", chunk => { invoked = true; return Task.CompletedTask; }, CancellationToken.None);
            invoked.Should().BeTrue();
        }
    }
}
