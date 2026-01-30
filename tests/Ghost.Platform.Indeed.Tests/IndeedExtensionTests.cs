using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Ghost.Hosting;
using Ghost.Abstractions;
using Moq;

namespace Ghost.Platform.Indeed.Tests;

public class IndeedExtensionTests
{
    [Fact]
    public void RegistersServices()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new[] { new KeyValuePair<string,string?>("Ghost:Extensions:Indeed:Enabled","true") }).Build();
        var services = new ServiceCollection();
        
        // Mock the required dependencies
        var proxyProviderMock = new Mock<IProxyProvider>();
        services.AddSingleton(proxyProviderMock.Object);
        
        // Use the proper Ghost hosting mechanism
        services.AddGhost(cfg, gw =>
        {
            gw.UseExtension(new Ghost.Platform.Indeed.IndeedExtension());
        });
        
        var sp = services.BuildServiceProvider();
        var jobClient = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();
        Assert.NotNull(jobClient);
    }
}
