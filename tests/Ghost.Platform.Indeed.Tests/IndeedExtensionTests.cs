using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Ghost.Hosting;
using Ghost.Abstractions;
using Ghost.Platform.Indeed.Internal;
using Moq;

namespace Ghost.Platform.Indeed.Tests;

public class IndeedExtensionTests
{
    [Fact]
    public void RegistersServices()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new[] { 
            new KeyValuePair<string,string?>("Ghost:Extensions:Indeed:Enabled", "true"),
            new KeyValuePair<string,string?>("Ghost:Extensions:Indeed:ApiKey", "test-api-key")
        }).Build();
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(new LoggerFactory());
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        
        var proxyProviderMock = new Mock<IProxyProvider>();
        services.AddSingleton(proxyProviderMock.Object);
        
        var loggerMock = new Mock<ILogger<IndeedApiClient>>();
        services.AddSingleton(loggerMock.Object);
        
        services.AddGhost(cfg, gw =>
        {
            gw.UseExtension(new Ghost.Platform.Indeed.IndeedExtension());
        });
        
        var sp = services.BuildServiceProvider();
        var jobClient = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();
        Assert.NotNull(jobClient);
    }
}
