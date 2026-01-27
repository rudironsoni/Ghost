using FluentAssertions;
using Xunit;

namespace Ghostwright.Hosting.Tests;

public class GhostwriterOptionsTests
{
    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var options = new GhostwriterOptions();
        options.ValidateExtensionDependencies.Should().BeTrue();
        options.Kernel.Should().NotBeNull();
    }

    [Fact]
    public void Kernel_CanBeModified()
    {
        var options = new GhostwriterOptions();
        options.Kernel.Headless = true;
        options.Kernel.Headless.Should().BeTrue();
    }

    [Fact]
    public void ValidateExtensionDependencies_CanBeDisabled()
    {
        var options = new GhostwriterOptions();
        options.ValidateExtensionDependencies = false;
        options.ValidateExtensionDependencies.Should().BeFalse();
    }
}
