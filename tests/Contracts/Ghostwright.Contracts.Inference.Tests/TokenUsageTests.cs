using FluentAssertions;
using Ghostwright.Contracts.Inference;
using Xunit;

namespace Ghostwright.Contracts.Inference.Tests;

public class TokenUsageTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var t = new TokenUsage();
        t.PromptTokens.Should().Be(0);
        t.CompletionTokens.Should().Be(0);
        t.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void Can_Set_And_Equality_Works()
    {
        var a = new TokenUsage { PromptTokens = 5, CompletionTokens = 10, TotalTokens = 15 };
        var b = new TokenUsage { PromptTokens = 5, CompletionTokens = 10, TotalTokens = 15 };
        a.Should().BeEquivalentTo(b);
    }
}
