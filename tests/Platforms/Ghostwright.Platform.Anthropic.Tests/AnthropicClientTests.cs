using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Ghostwright.Platform.Anthropic.Tests;

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

            var logger = Substitute.For<ILogger<AnthropicClient>>();
            var client = new AnthropicClient(mockSession, Microsoft.Extensions.Options.Options.Create(new AnthropicOptions()), logger);
            var req = new Ghostwright.Contracts.Inference.InferenceRequest { Messages = new[] { new Ghostwright.Contracts.Inference.InferenceMessage { Content = "hello" } } };
            var result = await client.CompleteAsync(req, CancellationToken.None);
            result.Should().NotBeNull();
            result.Content.Should().Be("completed text");
        }

        [Fact]
        public async Task StreamAsync_InvokesCallback_WithMockedPage()
        {
            var mockSession = Substitute.For<IBrowserSession>();
            var mockPage = Substitute.For<IPage>();
            mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
                .Returns(ValueTask.FromResult(mockPage));

            // simulate streaming by returning incremental content when EvaluateAsync<string> is called
            mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromResult("streaming text"));

            var logger = Substitute.For<ILogger<AnthropicClient>>();
            var client = new AnthropicClient(mockSession, Microsoft.Extensions.Options.Options.Create(new AnthropicOptions()), logger);
            var received = false;
            var req = new Ghostwright.Contracts.Inference.InferenceRequest { Messages = new[] { new Ghostwright.Contracts.Inference.InferenceMessage { Content = "hello" } } };
            await foreach (var _ in client.StreamAsync(req, CancellationToken.None))
            {
                received = true;
                break;
            }
            received.Should().BeTrue();
        }
    }
