using FluentAssertions;
using Xunit;

namespace Ghost.Hosting.Tests;

public class GhostOptionsTests
{
    [Fact]
    public void DefaultValuesAreCorrect()
    {
        var options = new GhostOptions();
        options.ValidateExtensionDependencies.Should().BeTrue();
        options.Kernel.Should().NotBeNull();
    }

    [Fact]
    public void KernelCanBeModified()
    {
        var options = new GhostOptions();
        options.Kernel.Headless = true;
        options.Kernel.Headless.Should().BeTrue();
    }

    [Fact]
    public void ValidateExtensionDependenciesCanBeDisabled()
    {
        var options = new GhostOptions();
        options.ValidateExtensionDependencies = false;
        options.ValidateExtensionDependencies.Should().BeFalse();
    }
}
