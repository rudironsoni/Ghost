using System;
using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.Anthropic.Tests;

public class AnthropicExtensionTests
{
        [Fact]
        public void Name_ShouldContainAnthropic()
        {
            var ext = new AnthropicExtension();
            ext.Name.Should().NotBeNullOrEmpty();
            ext.Name.ToLowerInvariant().Should().Contain("anthropic");
        }

        [Fact]
        public void Version_ShouldBeSet()
        {
            var ext = new AnthropicExtension();
            ext.Version.Should().NotBeNull();
            ext.Version.Major.Should().BeGreaterOrEqualTo(1);
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
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            Action act = () => ext.ConfigureServices(services, configuration);
            act.Should().NotThrow();
        }
    }
