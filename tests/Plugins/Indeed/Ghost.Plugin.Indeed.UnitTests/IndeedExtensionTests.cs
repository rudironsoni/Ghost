using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Platform.Indeed.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ghost.Platform.Indeed.Tests;

public class IndeedExtensionTests
{
    [Fact]
    public void RegistersServices()
    {
        IConfigurationRoot cfg = new ConfigurationBuilder().AddInMemoryCollection(new[] {
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

        ServiceProvider sp = services.BuildServiceProvider();
        IJobClient? jobClient = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();
        Assert.NotNull(jobClient);

        var apiClient = sp.GetService<IndeedApiClient>();
        Assert.NotNull(apiClient);
    }
}
