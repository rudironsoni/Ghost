using FluentAssertions;
using Xunit;

namespace Ghost.Hosting.Tests;

public class ExtensionExceptionTests
{
    [Fact]
    public void Constructor_SetsExtensionName()
    {
        var ex = new ExtensionException("TestExt", "error message");
        ex.ExtensionName.Should().Be("TestExt");
    }

    [Fact]
    public void Constructor_SetsMessage()
    {
        var ex = new ExtensionException("TestExt", "specific error");
        ex.Message.Should().Be("specific error");
    }
}
