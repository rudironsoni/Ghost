using Ghost.Contracts.Inference;
using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.Anthropic;

/// <summary>
/// Anthropic plugin providing AI inference capabilities.
/// </summary>
public sealed class AnthropicPlugin : IExtension
{
    /// <inheritdoc />
    public string Name => "Anthropic";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(IInferenceClient) };

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => Type.EmptyTypes;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new AnthropicPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:Anthropic:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:Anthropic:RegisterReadinessServices", true),
            RegisterKeyedInferenceClient = configuration.GetValue("Ghost:Plugins:Anthropic:RegisterKeyedInferenceClient", true)
        };

        // Register core services
        services.Configure<AnthropicOptions>(configuration.GetSection("Anthropic"));
        services.AddSingleton<AnthropicClient>();

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
            services.AddKeyedScoped<IInferenceClient>("anthropic", (sp, _) =>
                sp.GetRequiredService<AnthropicClient>());
        }
    }
}
