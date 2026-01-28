using FluentAssertions;
using Ghost.Contracts.Inference;
using Xunit;

namespace Ghost.Contracts.Inference.Tests;

public class InferenceChunkTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var c = new InferenceChunk();
        c.Delta.Should().BeEmpty();
        c.FinishReason.Should().BeNull();
    }

    [Fact]
    public void Equality_Works()
    {
        var a = new InferenceChunk { Delta = "d", FinishReason = "f" };
        var b = new InferenceChunk { Delta = "d", FinishReason = "f" };
        a.Should().BeEquivalentTo(b);
    }
}
