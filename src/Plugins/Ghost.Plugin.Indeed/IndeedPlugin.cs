using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.Indeed;

/// <summary>
/// Indeed plugin that wraps the platform extension with plugin metadata and capabilities.
/// </summary>
public sealed class IndeedPlugin : IExtension
{
    private readonly Ghost.Platform.Indeed.IndeedExtension _platformExtension;

    public IndeedPlugin()
    {
        _platformExtension = new Ghost.Platform.Indeed.IndeedExtension();
    }

    /// <inheritdoc />
    public string Name => "Indeed";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => _platformExtension.ProvidedServices;

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => _platformExtension.RequiredServices;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new IndeedPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:Indeed:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:Indeed:RegisterReadinessServices", true),
            RegisterKeyedJobClient = configuration.GetValue("Ghost:Plugins:Indeed:RegisterKeyedJobClient", true)
        };

        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

        if (pluginOptions.RegisterReadinessServices)
        {
            // Register plugin-specific services
            services.AddSingleton<IndeedPluginCapabilities>(sp => new IndeedPluginCapabilities
            {
                RequiresBrowser = true,
                RequiresProxy = false,
                SupportsJobs = true,
                SupportsSocial = false,
                SupportsNews = false
            });

            services.AddSingleton<IIndeedPluginReadinessCheck, IndeedPluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedJobClient)
        {
            // Register keyed IJobClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("indeed", (sp, _) =>
                sp.GetRequiredService<Ghost.Platform.Indeed.IndeedJobClient>());
        }
    }
}
