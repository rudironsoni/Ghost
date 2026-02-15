using Ghost.Utilities;
using Xunit;

namespace Ghost.Kernel.Tests;

public class DeduplicationServiceTests
{
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
