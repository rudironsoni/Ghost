using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Hosting;
using Ghost.Http;
using System.Net;

namespace Ghost.Platform.Google;

/// <summary>
/// Registers the Google/Gemini extension.
/// </summary>
public sealed class GoogleExtension : Ghost.Hosting.IExtension
{
    public string Name => "Google";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Inference.IInferenceClient), typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // diagnostic logging to help determine whether extension is applied and options bound
        try
        {
            Console.WriteLine("Configuring GoogleExtension...");
            Console.Out.Flush();
        }
        catch { }

        // bind using configuration section
        services.Configure<GoogleOptions>(configuration.GetSection("Ghost:Extensions:Google"));

        var rootOpts = new GoogleOptions();
        configuration.GetSection("Ghost:Extensions:Google").Bind(rootOpts);
        try
        {
            Console.WriteLine($"Google options: Jobs.Enabled = {rootOpts.Jobs?.Enabled}");
        }
        catch { }

        // Gemini
        if (rootOpts.Gemini == null || rootOpts.Gemini.Enabled)
        {
            services.Configure<Gemini.GeminiOptions>(configuration.GetSection("Ghost:Extensions:Google:Gemini"));
            services.AddScoped<Ghost.Contracts.Inference.IInferenceClient, Gemini.GeminiClient>();
        }

        // Google Jobs
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
                .ConfigurePrimaryHttpMessageHandler(() =>
                {
                    var handler = new HttpClientHandler
                    {
                        CookieContainer = new CookieContainer(),
                        UseCookies = true,
                        AllowAutoRedirect = true
                    };
                    return HttpClientSecurityExtensions.ConfigureSecureHttpClientHandler(handler);
                });
            services.AddScoped<Jobs.GoogleJobClient>();
            services.AddScoped<Ghost.Abstractions.IJobScraper, Jobs.GoogleJobClient>();
            services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => (Ghost.Contracts.Jobs.IJobClient)sp.GetRequiredService<Jobs.GoogleJobClient>());
        }
    }
}
