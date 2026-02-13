using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.Google;

/// <summary>
/// Google plugin that wraps the platform extension with plugin metadata and capabilities.
/// </summary>
public sealed class GooglePlugin : IExtension
{
    private readonly Ghost.Platform.Google.GoogleExtension _platformExtension;

    public GooglePlugin()
    {
        _platformExtension = new Ghost.Platform.Google.GoogleExtension();
    }

    /// <inheritdoc />
    public string Name => "Google";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => _platformExtension.ProvidedServices;

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => _platformExtension.RequiredServices;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new GooglePluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:Google:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:Google:RegisterReadinessServices", true),
            RegisterKeyedInferenceClient = configuration.GetValue("Ghost:Plugins:Google:RegisterKeyedInferenceClient", true)
        };

        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

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
                sp.GetRequiredService<Ghost.Platform.Google.Gemini.GeminiClient>());
        }
    }
}
