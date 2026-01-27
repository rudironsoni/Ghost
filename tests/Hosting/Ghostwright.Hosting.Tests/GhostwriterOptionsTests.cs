using FluentAssertions;
using Xunit;

namespace Ghostwright.Hosting.Tests
{
    public class GhostwriterOptionsTests
    {
        [Fact]
        public void DefaultValues_AreCorrect()
        {
            var opts = new GhostwriterOptions();
            opts.Kernel.Should().Be("DefaultKernel");
            opts.ValidateExtensionDependencies.Should().BeTrue();
        }

        [Fact]
        public void Kernel_CanBeModified()
        {
            var opts = new GhostwriterOptions { Kernel = "X" };
            opts.Kernel.Should().Be("X");
        }

        [Fact]
        public void ValidateExtensionDependencies_DefaultTrue()
        {
            var opts = new GhostwriterOptions();
            opts.ValidateExtensionDependencies.Should().BeTrue();
        }
    }
}
