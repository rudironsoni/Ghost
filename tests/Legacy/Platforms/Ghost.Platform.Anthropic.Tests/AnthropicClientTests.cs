using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ghost;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;
using Moq;
using Xunit;

namespace Ghost.Platform.Anthropic.Tests;

[Trait("Category", "Unit")]
public class AnthropicClientTests
{
    [Fact]
    public async Task CompleteAsyncSucceedsWithMockedSession()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);

        // simulate a page evaluation returning a string completion
        mockPage.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("completed text");

        var loggerMock = new Mock<ILogger<AnthropicClient>>();
        var client = new AnthropicClient(mockSession.Object, Microsoft.Extensions.Options.Options.Create(new AnthropicOptions()), loggerMock.Object);
        var req = new Ghost.Contracts.Inference.InferenceRequest { Messages = new[] { new Ghost.Contracts.Inference.InferenceMessage { Content = "hello" } } };
        var result = await client.CompleteAsync(req, CancellationToken.None);
        result.Should().NotBeNull();
        result.Content.Should().Be("completed text");
    }

    [Fact]
    public async Task StreamAsyncInvokesCallbackWithMockedPage()
    {
        var mockSession = new Mock<IBrowserSession>();
        var mockPage = new Mock<IPage>();
        mockSession.Setup(s => s.NewPageAsync(It.IsAny<PageOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(mockPage.Object);

        // simulate streaming by returning incremental content when EvaluateAsync<string> is called
        mockPage.Setup(p => p.EvaluateAsync<string>(It.IsAny<string>(), It.IsAny<object?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("streaming text");

        var loggerMock = new Mock<ILogger<AnthropicClient>>();
        var client = new AnthropicClient(mockSession.Object, Microsoft.Extensions.Options.Options.Create(new AnthropicOptions()), loggerMock.Object);
        var received = false;
        var req = new Ghost.Contracts.Inference.InferenceRequest { Messages = new[] { new Ghost.Contracts.Inference.InferenceMessage { Content = "hello" } } };
        await foreach (var _ in client.StreamAsync(req, CancellationToken.None))
        {
            received = true;
            break;
        }
        received.Should().BeTrue();
    }
}
