using System;
using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.Anthropic.Tests
{
    public class AnthropicExtensionTests
    {
        [Fact]
        public void Name_ShouldContainAnthropic()
        {
            var ext = new AnthropicExtension();
            ext.Name.Should().NotBeNullOrEmpty();
            ext.Name.Should().Contain("Anthropic", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Version_ShouldBeSet()
        {
            var ext = new AnthropicExtension();
            ext.Version.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void ProvidedServices_And_RequiredServices_AreConsistent()
        {
            var ext = new AnthropicExtension();
            ext.ProvidedServices.Should().NotBeNull();
            ext.RequiredServices.Should().NotBeNull();
        }

        [Fact]
        public void ConfigureServices_DoesNotThrow()
        {
            var ext = new AnthropicExtension();
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Action act = () => ext.ConfigureServices(services);
            act.Should().NotThrow();
        }
    }
}
