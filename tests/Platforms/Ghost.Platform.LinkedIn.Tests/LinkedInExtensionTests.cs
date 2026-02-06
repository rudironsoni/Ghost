using FluentAssertions;
using Ghost.Contracts.Jobs;
using Ghost.Contracts.News;
using Ghost.Contracts.Social;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.LinkedIn.Tests;

public class LinkedInExtensionTests
{
    [Fact]
    public void ProvidesAllClients()
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
