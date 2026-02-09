using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost;
using Ghost.Platform.Google.Gemini;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Platform.Google.Tests;

[Collection("GooglePlatformTests")]
public class GoogleClientTests
{
    [Fact]
    public async Task CompleteAsyncReturnsTextWhenPageEvaluates()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);

        mockPage.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("ok");

        var loggerMock = new Mock<ILogger<GeminiClient>>();
        var client = new GeminiClient(mockSession.Object, Options.Create(new GeminiOptions()), loggerMock.Object);
        var req = new Ghost.Contracts.Inference.InferenceRequest { Messages = new[] { new Ghost.Contracts.Inference.InferenceMessage { Content = "p" } } };
        var resp = await client.CompleteAsync(req, CancellationToken.None);
        resp.Content.Should().Be("ok");
    }

    [Fact]
    public async Task StreamAsyncInvokesHandler()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);
        mockPage.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("streaming");

        var loggerMock = new Mock<ILogger<GeminiClient>>();
        var client = new GeminiClient(mockSession.Object, Options.Create(new GeminiOptions()), loggerMock.Object);
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
