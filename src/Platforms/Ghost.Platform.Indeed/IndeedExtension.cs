using Microsoft.Extensions.DependencyInjection;
using Ghost.Models;
using Microsoft.Extensions.Configuration;
using Ghost.Platform.Indeed.Internal;
using Ghost.Contracts;
using Ghost.Contracts.Jobs;
using Ghost.Abstractions;
using Ghost.Http;
using System.Net.Http;

namespace Ghost.Platform.Indeed;

public class IndeedExtension : Ghost.Contracts.IExtension
{
    public string Name => "Indeed";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<IndeedOptions>(configuration.GetSection("Indeed"));
        var opts = configuration.GetSection("Indeed").Get<IndeedOptions>() ?? new IndeedOptions();

        if (!opts.Enabled) return;

        // register IndeedOptions for ApiClient constructor
        services.AddSingleton(opts);

        services.AddHttpClient<IndeedApiClient>(client => { client.Timeout = System.TimeSpan.FromSeconds(30); })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var provider = sp.GetRequiredService<IProxyProvider>();
                return new HttpClientHandler
                {
                    Proxy = new RotatingWebProxy(provider),
                    UseProxy = true
                };
            });

        services.AddSingleton<IndeedJobClient>();
        // register as both IJobScraper and IJobClient for backward compatibility
        services.AddSingleton<Ghost.Abstractions.IJobScraper>(sp => sp.GetRequiredService<IndeedJobClient>());
        services.AddSingleton<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<IndeedJobClient>());
    }
}
