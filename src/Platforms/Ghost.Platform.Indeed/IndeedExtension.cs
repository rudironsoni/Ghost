using Microsoft.Extensions.DependencyInjection;
using Ghost.Models;
using Microsoft.Extensions.Configuration;
using Ghost.Platform.Indeed.Internal;
using Ghost.Contracts;
using Ghost.Hosting;
using Ghost.Contracts.Jobs;
using Ghost.Abstractions;
using Ghost.Http;
using System.Net.Http;

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
        try { Console.WriteLine($"[DEBUG] IndeedExtension bound options: Country={opts.Country}"); } catch {}

        if (!opts.Enabled) return;

        // register IndeedOptions for ApiClient constructor
        services.AddSingleton(opts);

        // IndeedApiClient manages its own HttpClient per request to support dynamic proxy credentials.
        services.AddScoped<IndeedApiClient>();

        services.AddScoped<IndeedJobClient>();
        // register as both IJobScraper and IJobClient for backward compatibility
        services.AddScoped<Ghost.Abstractions.IJobScraper>(sp => sp.GetRequiredService<IndeedJobClient>());
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<IndeedJobClient>());
    }
}
