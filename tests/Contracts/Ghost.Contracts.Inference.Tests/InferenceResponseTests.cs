using FluentAssertions;
using Ghost.Contracts.Inference;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Contracts.Inference.Tests;

public class InferenceResponseTests : ReliabilityTestBase
{
    public InferenceResponseTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void DefaultsAreExpected()
    {
        var r = new InferenceResponse();
        r.Content.Should().BeEmpty();
        r.Model.Should().BeEmpty();
        r.FinishReason.Should().BeNull();
        r.Usage.Should().NotBeNull();
    }

    [Fact]
    public void EqualityWorks()
    {
        var a = new InferenceResponse { Content = "c", Model = "m", FinishReason = "f", Usage = new TokenUsage { PromptTokens = 1, CompletionTokens = 2, TotalTokens = 3 } };
        var b = new InferenceResponse { Content = "c", Model = "m", FinishReason = "f", Usage = new TokenUsage { PromptTokens = 1, CompletionTokens = 2, TotalTokens = 3 } };
        a.Should().BeEquivalentTo(b);
    }
}
