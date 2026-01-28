using FluentAssertions;
using Ghost.Contracts.Inference;
using Xunit;

namespace Ghost.Contracts.Inference.Tests;

public class InferenceMessageTests
{
    [Fact]
    public void Defaults_AreExpected()
    {
        var m = new InferenceMessage();
        m.Role.Should().Be(InferenceRole.User);
        m.Content.Should().BeEmpty();
    }

    [Fact]
    public void Equality_Works()
    {
        var a = new InferenceMessage { Role = InferenceRole.System, Content = "x" };
        var b = new InferenceMessage { Role = InferenceRole.System, Content = "x" };
        a.Should().BeEquivalentTo(b);
    }
}
