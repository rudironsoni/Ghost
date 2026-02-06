using FluentAssertions;
using Ghost.Contracts.Inference;
using Xunit;

namespace Ghost.Contracts.Inference.Tests;

public class InferenceChunkTests
{
    [Fact]
    public void DefaultsAreExpected()
    {
        var c = new InferenceChunk();
        c.Delta.Should().BeEmpty();
        c.FinishReason.Should().BeNull();
    }

    [Fact]
    public void EqualityWorks()
    {
        var a = new InferenceChunk { Delta = "d", FinishReason = "f" };
        var b = new InferenceChunk { Delta = "d", FinishReason = "f" };
        a.Should().BeEquivalentTo(b);
    }
}
