using System.Net.Http;
using Ghost.Abstractions;
using Ghost.Contracts;
using Ghost.Contracts.Jobs;
using Ghost.Hosting;
using Ghost.Http;
using Ghost.Models;
using Ghost.Platform.Indeed.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Platform.Indeed;

public class IndeedExtension : Ghost.Hosting.IExtension
{
    public string Name => "Indeed";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // register validator before binding options (follows InfoJobs pattern)
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<IndeedOptions>, IndeedOptionsValidator>();
        services.Configure<IndeedOptions>(configuration.GetSection("Ghost:Extensions:Indeed"));
        var opts = configuration.GetSection("Ghost:Extensions:Indeed").Get<IndeedOptions>() ?? new IndeedOptions();
        try { Console.WriteLine($"[DEBUG] IndeedExtension bound options: Country={opts.Country}"); } catch { }

        if (!opts.Enabled) return;

        // register IndeedOptions for ApiClient constructor
        services.AddSingleton(opts);

        services.AddSingleton<IndeedApiClient>(sp =>
        {
            var proxyProvider = sp.GetService<IProxyProvider>();
            var sessionOrchestrator = sp.GetService<Ghost.Platform.Common.Session.ISessionOrchestrator>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<IndeedApiClient>>();
            var options = sp.GetRequiredService<IndeedOptions>();

            if (proxyProvider != null && sessionOrchestrator != null)
            {
                return new IndeedApiClient(proxyProvider, sessionOrchestrator, options, logger);
            }

            if (sessionOrchestrator != null)
            {
                return new IndeedApiClient(sessionOrchestrator, options, logger);
            }

            if (proxyProvider != null)
            {
                return new IndeedApiClient(proxyProvider, options, logger);
            }

            throw new InvalidOperationException("IndeedApiClient requires IProxyProvider or ISessionOrchestrator to be registered.");
        });

        services.AddScoped<IndeedJobClient>();
        // register as both IJobScraper and IJobClient for backward compatibility
        services.AddScoped<Ghost.Abstractions.IJobScraper>(sp => sp.GetRequiredService<IndeedJobClient>());
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<IndeedJobClient>());
    }
}
