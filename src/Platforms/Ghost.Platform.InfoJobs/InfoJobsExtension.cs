using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Hosting;
using Ghost.Http;

namespace Ghost.Platform.InfoJobs;

/// <summary>
/// Registers the InfoJobs extension.
/// </summary>
public sealed class InfoJobsExtension : Ghost.Hosting.IExtension
{
    public string Name => "InfoJobs";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // diagnostic logging to help determine whether extension is applied and options bound
        try
        {
            Console.WriteLine("Configuring InfoJobsExtension...");
            Console.Out.Flush();
        }
        catch { }

        // bind using configuration section
        services.Configure<Jobs.InfoJobsOptions>(configuration.GetSection("Ghost:Extensions:InfoJobs"));

        var rootOpts = new Jobs.InfoJobsOptions();
        configuration.GetSection("Ghost:Extensions:InfoJobs").Bind(rootOpts);
        try
        {
            Console.WriteLine($"InfoJobs options: Enabled = {rootOpts.Enabled}");
        }
        catch { }

        // InfoJobs Job Client
        if (rootOpts.Enabled)
        {
            try { Console.WriteLine("Registering InfoJobClient..."); } catch { }
            services.AddHttpClient<Jobs.Internal.InfoJobsApiClient>()
                .AddTypedClient((httpClient, sp) =>
                {
                    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Jobs.InfoJobsOptions>>().Value;
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Jobs.Internal.InfoJobsApiClient>>();
                    return new Jobs.Internal.InfoJobsApiClient(httpClient, options, logger);
                })
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var handler = new System.Net.Http.HttpClientHandler
                    {
                        CookieContainer = new System.Net.CookieContainer(),
                        UseCookies = true,
                        AllowAutoRedirect = true
                    };
                    return HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);
                });
            services.AddScoped<Jobs.InfoJobClient>();
            services.AddScoped<Ghost.Abstractions.IJobScraper, Jobs.InfoJobClient>();
            services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => (Ghost.Contracts.Jobs.IJobClient)sp.GetRequiredService<Jobs.InfoJobClient>());
        }
    }
}