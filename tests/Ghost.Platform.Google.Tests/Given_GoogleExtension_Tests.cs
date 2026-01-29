using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Ghost.Platform.Google.Tests;

public class Given_GoogleExtension_Tests
{
    [Fact]
    public void RegistersServices_WhenEnabled()
    {
        var inMemory = new Dictionary<string, string>
        {
            { "Google:Gemini:Enabled", "true" },
            { "Google:Jobs:Enabled", "true" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var services = new ServiceCollection();
        var ext = new Ghost.Platform.Google.GoogleExtension();
        ext.ConfigureServices(services, configuration);

        var sp = services.BuildServiceProvider();
        var g = sp.GetService<Ghost.Contracts.Inference.IInferenceClient>();
        var j = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();

        Assert.NotNull(g);
        Assert.NotNull(j);
    }
}
