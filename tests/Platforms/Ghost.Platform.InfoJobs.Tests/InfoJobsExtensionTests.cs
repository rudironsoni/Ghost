using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ghost.Platform.InfoJobs.Tests;

public class InfoJobsExtensionTests
{
    [Fact]
    public void RegistersServicesWhenEnabled()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Ghost:Extensions:InfoJobs:Enabled", "true" },
            { "Ghost:Extensions:InfoJobs:ClientId", "test-client-id" },
            { "Ghost:Extensions:InfoJobs:ClientSecret", "test-client-secret" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var services = new ServiceCollection();

        var ext = new Ghost.Platform.InfoJobs.InfoJobsExtension();
        ext.ConfigureServices(services, configuration);

        var sp = services.BuildServiceProvider();
        var jobClient = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();

        Assert.NotNull(jobClient);
    }
}
