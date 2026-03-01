using FluentAssertions;
using Ghost.Contracts.Inference;
using Ghost.Testing.Reliability;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Contracts.Inference.Tests;

public class InferenceRoleTests : ReliabilityTestBase
{
    public InferenceRoleTests(ITestOutputHelper output) : base(output) { }
    [Theory]
    [InlineData(InferenceRole.System)]
    [InlineData(InferenceRole.User)]
    [InlineData(InferenceRole.Assistant)]
    public void EnumValuesAvailable(InferenceRole r)
    {
        r.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
