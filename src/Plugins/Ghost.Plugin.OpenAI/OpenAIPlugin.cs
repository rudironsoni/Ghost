using Ghost.Contracts.Inference;
using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.OpenAI;

/// <summary>
/// OpenAI plugin providing AI inference capabilities.
/// </summary>
public sealed class OpenAIPlugin : IExtension
{
    /// <inheritdoc />
    public string Name => "OpenAI";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => new[] { typeof(IInferenceClient) };

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => new[] { typeof(Ghost.IBrowserSession) };

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new OpenAIPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:OpenAI:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:OpenAI:RegisterReadinessServices", true),
            RegisterKeyedInferenceClient = configuration.GetValue("Ghost:Plugins:OpenAI:RegisterKeyedInferenceClient", true)
        };

        // Register core services
        services.Configure<OpenAIOptions>(configuration.GetSection("OpenAI"));
        services.AddScoped<IInferenceClient, OpenAIClient>();

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
            services.AddKeyedScoped<IInferenceClient>("openai", (sp, _) =>
                sp.GetRequiredService<OpenAIClient>());
        }
    }
}
