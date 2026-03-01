using FluentAssertions;
using Ghost.Contracts.Inference;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Contracts.Inference.Tests;

public class TokenUsageTests : ReliabilityTestBase
{
    public TokenUsageTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void DefaultsAreExpected()
    {
        var t = new TokenUsage();
        t.PromptTokens.Should().Be(0);
        t.CompletionTokens.Should().Be(0);
        t.TotalTokens.Should().Be(0);
    }

    [Fact]
    public void CanSetAndEqualityWorks()
    {
        var a = new TokenUsage { PromptTokens = 5, CompletionTokens = 10, TotalTokens = 15 };
        var b = new TokenUsage { PromptTokens = 5, CompletionTokens = 10, TotalTokens = 15 };
        a.Should().BeEquivalentTo(b);
    }
}
