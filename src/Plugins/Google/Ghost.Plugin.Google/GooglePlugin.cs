using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Google;

/// <summary>
/// Google plugin that provides Google services including Jobs and Gemini inference.
/// </summary>
public sealed class GooglePlugin : IExtension
{
    /// <inheritdoc />
    public string Name => "Google";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => new[]
    {
        typeof(GooglePluginCapabilities),
        typeof(IGooglePluginReadinessCheck),
        typeof(Jobs.GoogleJobClient),
        typeof(Gemini.GeminiClient)
    };

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => Array.Empty<Type>();

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new GooglePluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:Google:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:Google:RegisterReadinessServices", true),
            RegisterKeyedInferenceClient = configuration.GetValue("Ghost:Plugins:Google:RegisterKeyedInferenceClient", true)
        };

        // Configure Google options
        services.Configure<GoogleOptions>(configuration.GetSection("Ghost:Plugins:Google"));
        services.AddSingleton<IValidateOptions<GoogleOptions>, GoogleOptionsValidator>();

        // Configure Jobs options
        services.Configure<Jobs.GoogleJobsOptions>(configuration.GetSection("Ghost:Plugins:Google:Jobs"));

        // Configure Gemini options
        services.Configure<Gemini.GeminiOptions>(configuration.GetSection("Ghost:Plugins:Google:Gemini"));

        // Register Jobs client
        services.AddSingleton<Jobs.GoogleJobClient>();

        // Register Gemini client
        services.AddSingleton<Gemini.GeminiClient>();

        if (pluginOptions.RegisterReadinessServices)
        {
            // Register plugin-specific services
            services.AddSingleton<GooglePluginCapabilities>(sp => new GooglePluginCapabilities
            {
                RequiresBrowser = false,
                RequiresProxy = false,
                SupportsJobs = true,
                SupportsSocial = false,
                SupportsNews = false,
                SupportsInference = true
            });

            services.AddSingleton<IGooglePluginReadinessCheck, GooglePluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedInferenceClient)
        {
            // Register keyed IInferenceClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Inference.IInferenceClient>("google", (sp, _) =>
                sp.GetRequiredService<Ghost.Plugin.Google.Gemini.GeminiClient>());
        }
    }
}
