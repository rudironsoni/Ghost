using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Ghost.Contracts.Social;
using Ghost.Contracts.Jobs;
using Ghost.Contracts.News;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInExtensionTests
{
    [Fact]
    public void Provides_All_Clients()
    {
            var ext = new LinkedInExtension();
            var services = new ServiceCollection();
            var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build();
            ext.ConfigureServices(services, config);

        var provided = ext.ProvidedServices;
        provided.Should().Contain(typeof(ISocialClient));
        provided.Should().Contain(typeof(IJobClient));
        provided.Should().Contain(typeof(INewsClient));
    }
}
