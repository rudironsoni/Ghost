using FluentAssertions;
using Ghostwright.Contracts.Inference;
using Xunit;

namespace Ghostwright.Contracts.Inference.Tests;

public class InferenceResponseTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var r = new InferenceResponse();
        r.Content.Should().BeEmpty();
        r.Model.Should().BeEmpty();
        r.FinishReason.Should().BeNull();
        r.Usage.Should().NotBeNull();
    }

    [Fact]
    public void Equality_Works()
    {
        var a = new InferenceResponse { Content = "c", Model = "m", FinishReason = "f", Usage = new TokenUsage { PromptTokens = 1, CompletionTokens = 2, TotalTokens = 3 } };
        var b = new InferenceResponse { Content = "c", Model = "m", FinishReason = "f", Usage = new TokenUsage { PromptTokens = 1, CompletionTokens = 2, TotalTokens = 3 } };
        a.Should().BeEquivalentTo(b);
    }
}
