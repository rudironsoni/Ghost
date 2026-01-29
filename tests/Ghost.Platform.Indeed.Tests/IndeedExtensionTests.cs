using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghost.Platform.Indeed.Tests;

public class IndeedExtensionTests
{
    [Fact]
    public void RegistersServices()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new[] { new KeyValuePair<string,string?>("Indeed:Enabled","true") }).Build();
        var services = new ServiceCollection();
        services.AddIndeed(cfg);
        var sp = services.BuildServiceProvider();
        var jobClient = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();
        Assert.NotNull(jobClient);
    }
}
