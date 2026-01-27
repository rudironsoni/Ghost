using FluentAssertions;
using Ghostwright.Contracts.Inference;
using Xunit;

namespace Ghostwright.Contracts.Inference.Tests;

public class InferenceRoleTests
{
    [Theory]
    [InlineData(InferenceRole.System)]
    [InlineData(InferenceRole.User)]
    [InlineData(InferenceRole.Assistant)]
    public void Enum_Values_Available(InferenceRole r)
    {
        r.ToString().Should().NotBeNullOrWhiteSpace();
    }
}
