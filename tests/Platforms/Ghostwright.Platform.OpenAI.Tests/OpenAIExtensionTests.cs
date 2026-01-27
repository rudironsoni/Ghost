using System;
using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.OpenAI.Tests
{
    public class OpenAIExtensionTests
    {
        [Fact]
        public void Name_ShouldContainOpenAI()
        {
            var ext = new OpenAIExtension();
            ext.Name.Should().NotBeNullOrEmpty();
            ext.Name.Should().Contain("OpenAI", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigureServices_DoesNotThrow()
        {
            var ext = new OpenAIExtension();
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Action act = () => ext.ConfigureServices(services);
            act.Should().NotThrow();
        }
    }
}
