using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ghostwright.Platform.Anthropic.Tests
{
    public class AnthropicClientTests
    {
        [Fact]
        public async Task CompleteAsync_Succeeds_WithMockedSession()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));

            // simulate a page evaluation returning a string completion
            mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("completed text"));

            var client = new AnthropicClient(mockSession, new AnthropicOptions());
            var result = await client.CompleteAsync("hello", CancellationToken.None);
            result.Should().NotBeNull();
            result.Text.Should().Be("completed text");
        }

        [Fact]
        public async Task StreamAsync_InvokesCallback_WithMockedPage()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));

            // simulate streaming by invoking a provided handler when EvaluateAsync is called
            mockPage.EvaluateAsync(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(Task.CompletedTask)
                .AndDoes(ci => { /* no-op */ });

            var client = new AnthropicClient(mockSession, new AnthropicOptions());
            var received = false;
            await client.StreamAsync("hello", chunk => { received = true; return Task.CompletedTask; }, CancellationToken.None);
            received.Should().BeTrue();
        }
    }
}
