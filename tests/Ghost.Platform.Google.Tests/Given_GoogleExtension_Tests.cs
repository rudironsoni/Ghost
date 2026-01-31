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
            { "Ghost:Extensions:Google:Gemini:Enabled", "true" },
            { "Ghost:Extensions:Google:Jobs:Enabled", "true" }
        };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var services = new ServiceCollection();
        
        // Mock required services
        services.AddSingleton(Substitute.For<Ghost.IBrowserSession>());
        
        var ext = new Ghost.Platform.Google.GoogleExtension();
        ext.ConfigureServices(services, configuration);

        // Override GoogleJobClient registration to use API-only constructor so tests don't require GhostKernel
        services.AddScoped<Jobs.GoogleJobClient>(sp =>
        {
            var api = sp.GetRequiredService<Jobs.Internal.GoogleJobsApiClient>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Jobs.GoogleJobClient>>();
            var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Jobs.GoogleJobsOptions>>();
            return new Jobs.GoogleJobClient(api, logger, opts);
        });

        var sp = services.BuildServiceProvider();
        var g = sp.GetService<Ghost.Contracts.Inference.IInferenceClient>();
        var j = sp.GetService<Ghost.Contracts.Jobs.IJobClient>();

        Assert.NotNull(g);
        Assert.NotNull(j);
    }
}
