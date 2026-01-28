using FluentAssertions;
using Xunit;

namespace Ghost.Hosting.Tests;

public class GhostOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new GhostOptions();
        options.ValidateExtensionDependencies.Should().BeTrue();
        options.Kernel.Should().NotBeNull();
    }

    [Fact]
    public void Kernel_CanBeModified()
    {
        var options = new GhostOptions();
        options.Kernel.Headless = true;
        options.Kernel.Headless.Should().BeTrue();
    }

    [Fact]
    public void ValidateExtensionDependencies_CanBeDisabled()
    {
        var options = new GhostOptions();
        options.ValidateExtensionDependencies = false;
        options.ValidateExtensionDependencies.Should().BeFalse();
    }
}
