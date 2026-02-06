using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Hosting;
using Ghost.Http;
using System.Net;

namespace Ghost.Platform.Google;

public sealed class GoogleExtension : Ghost.Hosting.IExtension
{
    public string Name => "Google";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Inference.IInferenceClient), typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        try
        {
            Console.WriteLine("Configuring GoogleExtension...");
            Console.Out.Flush();
        }
        catch { }

        services.Configure<GoogleOptions>(configuration.GetSection("Ghost:Extensions:Google"));
        services.AddSingleton<Microsoft.Extensions.Options.IValidateOptions<GoogleOptions>, GoogleOptionsValidator>();

        var rootOpts = new GoogleOptions();
        configuration.GetSection("Ghost:Extensions:Google").Bind(rootOpts);
        try
        {
            Console.WriteLine($"Google options: Jobs.Enabled = {rootOpts.Jobs?.Enabled}");
        }
        catch { }

        if (rootOpts.Gemini == null || rootOpts.Gemini.Enabled)
        {
            services.Configure<Gemini.GeminiOptions>(configuration.GetSection("Ghost:Extensions:Google:Gemini"));
            services.AddScoped<Ghost.Contracts.Inference.IInferenceClient, Gemini.GeminiClient>();
        }

        if (rootOpts.Jobs == null || rootOpts.Jobs.Enabled)
        {
            try { Console.WriteLine("Registering GoogleJobClient..."); } catch { }
            services.Configure<Jobs.GoogleJobsOptions>(configuration.GetSection("Ghost:Extensions:Google:Jobs"));
            services.AddHttpClient<Jobs.Internal.GoogleJobsApiClient>()
            .AddTypedClient((httpClient, sp) =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Jobs.GoogleJobsOptions>>().Value;
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Jobs.Internal.GoogleJobsApiClient>>();
                return new Jobs.Internal.GoogleJobsApiClient(httpClient, options, logger);
            })
            .ConfigurePrimaryHttpMessageHandler(sp =>
            {
                var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Jobs.GoogleJobsOptions>>().Value;
                // Force disable proxy for Google Jobs - rely on kernel-level proxy or direct connection
                // Session-level SOCKS5 proxies cause authentication issues with Playwright
                var handler = new HttpClientHandler { CookieContainer = new CookieContainer(), UseCookies = true, AllowAutoRedirect = true };
                return HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);
            });

            services.AddScoped<Jobs.Internal.GoogleJobsBrowserClient>();
            services.AddScoped<Jobs.Internal.GoogleJobsScraper>();
            // Register GoogleJobClient with both ApiClient and BrowserClient for full strategy support
            services.AddScoped<Jobs.GoogleJobClient>(sp =>
            {
                var apiClient = sp.GetRequiredService<Jobs.Internal.GoogleJobsApiClient>();
                var browserClient = sp.GetRequiredService<Jobs.Internal.GoogleJobsBrowserClient>();
                var scraper = sp.GetRequiredService<Jobs.Internal.GoogleJobsScraper>();
                var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Jobs.GoogleJobClient>>();
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Jobs.GoogleJobsOptions>>();
                return new Jobs.GoogleJobClient(apiClient, browserClient, scraper, logger, options);
            });
            // Register as both IJobScraper (for aggregator) and IJobClient (for backward compatibility)
            services.AddScoped<Ghost.Abstractions.IJobScraper>(sp => sp.GetRequiredService<Jobs.GoogleJobClient>());
            services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<Jobs.GoogleJobClient>());
        }
    }
}