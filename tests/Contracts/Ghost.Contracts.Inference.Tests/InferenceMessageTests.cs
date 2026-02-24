using FluentAssertions;
using Ghost.Contracts.Inference;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Contracts.Inference.Tests;

public class InferenceMessageTests : ReliabilityTestBase
{
    public InferenceMessageTests(ITestOutputHelper output) : base(output) { }
    [Fact]
    public void DefaultsAreExpected()
    {
        var m = new InferenceMessage();
        m.Role.Should().Be(InferenceRole.User);
        m.Content.Should().BeEmpty();
    }

    [Fact]
    public void EqualityWorks()
    {
        var a = new InferenceMessage { Role = InferenceRole.System, Content = "x" };
        var b = new InferenceMessage { Role = InferenceRole.System, Content = "x" };
        a.Should().BeEquivalentTo(b);
    }
}
