using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.OpenAI;

/// <summary>
/// OpenAI plugin that wraps the platform extension with plugin metadata and capabilities.
/// </summary>
public sealed class OpenAIPlugin : IExtension
{
    private readonly Ghost.Platform.OpenAI.OpenAIExtension _platformExtension;

    public OpenAIPlugin()
    {
        _platformExtension = new Ghost.Platform.OpenAI.OpenAIExtension();
    }

    /// <inheritdoc />
    public string Name => "OpenAI";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => _platformExtension.ProvidedServices;

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => _platformExtension.RequiredServices;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new OpenAIPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:OpenAI:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:OpenAI:RegisterReadinessServices", true),
            RegisterKeyedInferenceClient = configuration.GetValue("Ghost:Plugins:OpenAI:RegisterKeyedInferenceClient", true)
        };

        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

        if (pluginOptions.RegisterReadinessServices)
        {
            // Register plugin-specific services
            services.AddSingleton<OpenAIPluginCapabilities>(sp => new OpenAIPluginCapabilities
            {
                RequiresBrowser = false,
                RequiresProxy = false,
                SupportsJobs = false,
                SupportsSocial = false,
                SupportsNews = false,
                SupportsInference = true
            });

            services.AddSingleton<IOpenAIPluginReadinessCheck, OpenAIPluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedInferenceClient)
        {
            // Register keyed IInferenceClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Inference.IInferenceClient>("openai", (sp, _) =>
                sp.GetRequiredService<Ghost.Platform.OpenAI.OpenAIClient>());
        }
    }
}
