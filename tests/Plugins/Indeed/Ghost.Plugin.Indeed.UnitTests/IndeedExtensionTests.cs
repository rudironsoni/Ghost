using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Plugin.Indeed.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Ghost.Plugin.Indeed.Tests;

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
            gw.UseExtension(new Ghost.Plugin.Indeed.IndeedExtension());
        });

        ServiceProvider sp = services.BuildServiceProvider();
        Ghost.Contracts.Jobs.IJobClient? jobClient = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();
        Assert.NotNull(jobClient);

        Ghost.Plugin.Indeed.Internal.IndeedApiClient? apiClient = sp.GetService<Ghost.Plugin.Indeed.Internal.IndeedApiClient>();
        Assert.NotNull(apiClient);
    }
}
