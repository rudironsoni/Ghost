using System;
using FluentAssertions;
using Xunit;

namespace Ghostwright.Platform.Google.Tests
{
    public class GoogleExtensionTests
    {
        [Fact]
        public void Name_ShouldContainGoogle()
        {
            var ext = new GoogleExtension();
            ext.Name.Should().Contain("Google", StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ConfigureServices_DoesNotThrow()
        {
            var ext = new GoogleExtension();
            var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection();
            Action act = () => ext.ConfigureServices(services);
            act.Should().NotThrow();
        }
    }
}
