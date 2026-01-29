using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Platform.Google;

/// <summary>
/// Registers the Google/Gemini extension.
/// </summary>
public sealed class GoogleExtension : Ghost.Contracts.IExtension
{
    public string Name => "Google";
    public Version Version => new(1, 0, 0);
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(Ghost.Contracts.Inference.IInferenceClient), typeof(Ghost.Contracts.Jobs.IJobClient) };
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // bind using configuration section
        services.Configure<GoogleOptions>(configuration.GetSection("Google"));

        var rootOpts = new GoogleOptions();
        configuration.GetSection("Google").Bind(rootOpts);

        // Gemini
        if (rootOpts.Gemini == null || rootOpts.Gemini.Enabled)
        {
            services.Configure<Gemini.GeminiOptions>(configuration.GetSection("Google:Gemini"));
            services.AddScoped<Ghost.Contracts.Inference.IInferenceClient, Gemini.GeminiClient>();
        }

        // Google Jobs
        if (rootOpts.Jobs == null || rootOpts.Jobs.Enabled)
        {
            services.Configure<Jobs.GoogleJobsOptions>(configuration.GetSection("Google:Jobs"));
            services.AddHttpClient();
            services.AddScoped<Jobs.Internal.GoogleJobsApiClient>();
            services.AddScoped<Ghost.Abstractions.IJobScraper, Jobs.GoogleJobClient>();
            services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => (Ghost.Contracts.Jobs.IJobClient)sp.GetRequiredService<Jobs.GoogleJobClient>());
        }
    }
}
