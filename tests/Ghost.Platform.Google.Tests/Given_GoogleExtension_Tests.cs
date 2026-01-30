using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Xunit;
using NSubstitute;

namespace Ghost.Platform.Google.Tests;

public class GoogleExtensionTests
{
    [Fact]
    public void RegistersServicesWhenEnabled()
    {
        var inMemory = new Dictionary<string, string?>
        {
            { "Google:Gemini:Enabled", "true" },
            { "Google:Jobs:Enabled", "true" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var services = new ServiceCollection();
        
        // Mock required services
        services.AddSingleton(Substitute.For<Ghost.IBrowserSession>());
        
        var ext = new Ghost.Platform.Google.GoogleExtension();
        ext.ConfigureServices(services, configuration);

        var sp = services.BuildServiceProvider();
        var g = sp.GetService<Ghost.Contracts.Inference.IInferenceClient>();
        var j = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();

        Assert.NotNull(g);
        Assert.NotNull(j);
    }
}
