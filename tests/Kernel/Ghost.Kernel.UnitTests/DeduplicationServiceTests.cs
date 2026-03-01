using Ghost.Testing.Reliability;
using Ghost.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Ghost.Kernel.Tests;

public class DeduplicationServiceTests : ReliabilityTestBase
{
    public DeduplicationServiceTests(ITestOutputHelper output) : base(output) { }

    private readonly DeduplicationService _svc = new();

    [Fact]
    public void GenerateIdSameInputsConsistent()
    {
        var a = _svc.GenerateId("Senior Engineer", "Acme Inc");
        var b = _svc.GenerateId("senior engineer", "Acme Inc ");
        Assert.Equal(a, b);
    }

    [Fact]
    public void GenerateIdDifferentInputsDifferent()
    {
        var a = _svc.GenerateId("A", "B");
        var b = _svc.GenerateId("A", "C");
        Assert.NotEqual(a, b);
    }
}
