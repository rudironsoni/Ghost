using System;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghostwright.Platform.Google.Tests;

public class GoogleExtensionTests
{
        [Fact]
        public void Name_ShouldContainGoogle()
        {
            var ext = new GoogleExtension();
            ext.Name.ToLowerInvariant().Should().Contain("google");
        }

        [Fact]
        public void ConfigureServices_DoesNotThrow()
        {
            var ext = new GoogleExtension();
            var services = new ServiceCollection();
            var config = new ConfigurationBuilder().Build();
            Action act = () => ext.ConfigureServices(services, config);
            act.Should().NotThrow();
        }
    }
