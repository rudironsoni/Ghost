using FluentAssertions;
using Xunit;

namespace Ghost.Hosting.Tests;

public class ExtensionExceptionTests
{
    [Fact]
    public void ConstructorSetsExtensionName()
    {
        var ex = new ExtensionException("TestExt", "error message");
        ex.ExtensionName.Should().Be("TestExt");
    }

    [Fact]
    public void ConstructorSetsMessage()
    {
        var ex = new ExtensionException("TestExt", "specific error");
        ex.Message.Should().Be("specific error");
    }
}
