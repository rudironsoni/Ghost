using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.Anthropic;

/// <summary>
/// Anthropic plugin that wraps the platform extension with plugin metadata and capabilities.
/// </summary>
public sealed class AnthropicPlugin : IExtension
{
    private readonly Ghost.Platform.Anthropic.AnthropicExtension _platformExtension;

    public AnthropicPlugin()
    {
        _platformExtension = new Ghost.Platform.Anthropic.AnthropicExtension();
    }

    /// <inheritdoc />
    public string Name => "Anthropic";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => _platformExtension.ProvidedServices;

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => _platformExtension.RequiredServices;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new AnthropicPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:Anthropic:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:Anthropic:RegisterReadinessServices", true),
            RegisterKeyedInferenceClient = configuration.GetValue("Ghost:Plugins:Anthropic:RegisterKeyedInferenceClient", true)
        };

        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

        if (pluginOptions.RegisterReadinessServices)
        {
            // Register plugin-specific services
            services.AddSingleton<AnthropicPluginCapabilities>(sp => new AnthropicPluginCapabilities
            {
                RequiresBrowser = false,
                RequiresProxy = false,
                SupportsJobs = false,
                SupportsSocial = false,
                SupportsNews = false,
                SupportsInference = true
            });

            services.AddSingleton<IAnthropicPluginReadinessCheck, AnthropicPluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedInferenceClient)
        {
            // Register keyed IInferenceClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Inference.IInferenceClient>("anthropic", (sp, _) =>
                sp.GetRequiredService<Ghost.Platform.Anthropic.AnthropicClient>());
        }
    }
}
