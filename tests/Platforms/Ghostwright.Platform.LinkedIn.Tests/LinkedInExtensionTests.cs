using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghostwright.Platform.LinkedIn.Tests
{
    public class LinkedInExtensionTests
    {
        [Fact]
        public void Provides_All_Clients()
        {
            var ext = new LinkedInExtension();
            var services = new ServiceCollection();
            ext.ConfigureServices(services);

            var provided = ext.ProvidedServices;
            provided.Should().Contain(typeof(ISocialClient));
            provided.Should().Contain(typeof(IJobClient));
            provided.Should().Contain(typeof(INewsClient));
        }
    }
}
