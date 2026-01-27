using System;
using FluentAssertions;
using NSubstitute;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
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
            ext.Name.ToLowerInvariant().Should().Contain("openai");
        }

        [Fact]
        public void ConfigureServices_DoesNotThrow()
        {
            var ext = new OpenAIExtension();
            var services = new ServiceCollection();
            var config = Substitute.For<IConfiguration>();
            var section = Substitute.For<IConfigurationSection>();
            config.GetSection(Arg.Any<string>()).Returns(section);
            Action act = () => ext.ConfigureServices(services, config);
            act.Should().NotThrow();
        }
    }
}
