using Ghost.Hosting;
using Ghost.Http;
using Ghost.Plugin.InfoJobs.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.InfoJobs;

/// <summary>
/// InfoJobs plugin that provides job search and scraping capabilities.
/// </summary>
public sealed class InfoJobsPlugin : IExtension
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
            Console.WriteLine("Configuring InfoJobsPlugin...");
            Console.Out.Flush();
        }
        catch { }

        // bind using configuration section
        services.Configure<InfoJobsOptions>(configuration.GetSection("Ghost:Extensions:InfoJobs"));
        // register options validator
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<InfoJobsOptions>, InfoJobsOptionsValidator>();

        var rootOpts = new InfoJobsOptions();
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
            services.AddHttpClient<Internal.InfoJobsApiClient>()
                .AddTypedClient((httpClient, sp) =>
                {
                    InfoJobsOptions options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<InfoJobsOptions>>().Value;
                    ILogger<InfoJobsApiClient> logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Internal.InfoJobsApiClient>>();
                    return new Internal.InfoJobsApiClient(httpClient, options, logger);
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
            services.AddScoped<InfoJobClient>();
            services.AddScoped<Ghost.Abstractions.IJobScraper, InfoJobClient>();
            services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => (Ghost.Contracts.Jobs.IJobClient)sp.GetRequiredService<InfoJobClient>());
        }

        // Plugin-specific services
        var pluginOptions = new InfoJobsPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:InfoJobs:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:InfoJobs:RegisterReadinessServices", true),
            RegisterKeyedJobClient = configuration.GetValue("Ghost:Plugins:InfoJobs:RegisterKeyedJobClient", true)
        };

        if (pluginOptions.RegisterReadinessServices)
        {
            services.AddSingleton<InfoJobsPluginCapabilities>(sp => new InfoJobsPluginCapabilities
            {
                RequiresBrowser = true,
                RequiresProxy = false,
                SupportsJobs = true,
                SupportsSocial = false,
                SupportsNews = false
            });

            services.AddSingleton<IInfoJobsPluginReadinessCheck, InfoJobsPluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedJobClient)
        {
            // Register keyed IJobClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("infojobs", (sp, _) =>
                sp.GetRequiredService<InfoJobClient>());
        }
    }
}
