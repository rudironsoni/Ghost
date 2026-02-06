using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Contracts.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ghost.Platform.OpenAI.Tests;

public class OpenAIClientTests
{
    [Fact]
    public async Task CompleteAsync_ReturnsText_WhenPageEvaluates()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));

        mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("response text"));

        var logger = Substitute.For<ILogger<OpenAIClient>>();
        var client = new OpenAIClient(mockSession, Options.Create(new OpenAIOptions()), logger);
        var resp = await client.CompleteAsync(new InferenceRequest { Messages = new[] { new InferenceMessage { Content = "prompt" } } }, CancellationToken.None);
        resp.Content.Should().Be("response text");
    }

    [Fact]
    public async Task StreamAsync_CallsHandler()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));

        mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("stream chunk"));

        var logger = Substitute.For<ILogger<OpenAIClient>>();
        var client = new OpenAIClient(mockSession, Options.Create(new OpenAIOptions()), logger);
        var invoked = false;
        await foreach (var _ in client.StreamAsync(new InferenceRequest { Messages = new[] { new InferenceMessage { Content = "prompt" } } }, CancellationToken.None))
        {
            invoked = true;
            break;
        }
        invoked.Should().BeTrue();
    }
}
