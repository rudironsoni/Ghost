using FluentAssertions;
using Ghost.Contracts.Inference;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Contracts.Inference.Tests;

public class InferenceChunkTests : ReliabilityTestBase
{
    public InferenceChunkTests(ITestOutputHelper output) : base(output) { }
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
