using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost;
using Ghost.Contracts.Inference;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Plugin.OpenAI.Tests;

public class OpenAIClientTests
{
    [Fact]
    public async Task CompleteAsyncReturnsTextWhenPageEvaluates()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);

        mockPage.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("response text");

        var loggerMock = new Mock<ILogger<OpenAIClient>>();
        var client = new OpenAIClient(mockSession.Object, Options.Create(new OpenAIOptions()), loggerMock.Object);
        var resp = await client.CompleteAsync(new InferenceRequest { Messages = new[] { new InferenceMessage { Content = "prompt" } } }, CancellationToken.None);
        resp.Content.Should().Be("response text");
    }

    [Fact]
    public async Task StreamAsyncCallsHandler()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);

        mockPage.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("stream chunk");

        var loggerMock = new Mock<ILogger<OpenAIClient>>();
        var client = new OpenAIClient(mockSession.Object, Options.Create(new OpenAIOptions()), loggerMock.Object);
        var invoked = false;
        await foreach (var _ in client.StreamAsync(new InferenceRequest { Messages = new[] { new InferenceMessage { Content = "prompt" } } }, CancellationToken.None))
        {
            invoked = true;
            break;
        }
        invoked.Should().BeTrue();
    }
}
