using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost.Platform.Google.Gemini;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Ghost.Platform.Google.Tests;

public class GoogleClientTests
{
    [Fact]
    public async Task CompleteAsyncReturnsTextWhenPageEvaluates()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));

        mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("ok"));

        var logger = Substitute.For<ILogger<GeminiClient>>();
        var client = new GeminiClient(mockSession, Options.Create(new GeminiOptions()), logger);
        var req = new Ghost.Contracts.Inference.InferenceRequest { Messages = new[] { new Ghost.Contracts.Inference.InferenceMessage { Content = "p" } } };
        var resp = await client.CompleteAsync(req, CancellationToken.None);
        resp.Content.Should().Be("ok");
    }

    [Fact]
    public async Task StreamAsyncInvokesHandler()
    {
        var mockSession = Substitute.For<IBrowserSession>();
        var mockPage = Substitute.For<IPage>();
        mockSession.NewPageAsync(Arg.Any<PageOptions>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(mockPage));
        mockPage.EvaluateAsync<string>(Arg.Any<string>(), Arg.Any<object?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult("streaming"));

        var logger = Substitute.For<ILogger<GeminiClient>>();
        var client = new GeminiClient(mockSession, Options.Create(new GeminiOptions()), logger);
        var called = false;
        var req = new Ghost.Contracts.Inference.InferenceRequest { Messages = new[] { new Ghost.Contracts.Inference.InferenceMessage { Content = "p" } } };
        await foreach (var _ in client.StreamAsync(req, CancellationToken.None))
        {
            called = true;
            break;
        }
        called.Should().BeTrue();
    }
}
