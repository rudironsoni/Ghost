using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.Glassdoor;

/// <summary>
/// Glassdoor plugin that wraps the platform extension with plugin metadata and capabilities.
/// </summary>
public sealed class GlassdoorPlugin : IExtension
{
    private readonly Ghost.Platform.Glassdoor.GlassdoorExtension _platformExtension;

    public GlassdoorPlugin()
    {
        _platformExtension = new Ghost.Platform.Glassdoor.GlassdoorExtension();
    }

    /// <inheritdoc />
    public string Name => "Glassdoor";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => _platformExtension.ProvidedServices;

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => _platformExtension.RequiredServices;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new GlassdoorPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:Glassdoor:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:Glassdoor:RegisterReadinessServices", true),
            RegisterKeyedJobClient = configuration.GetValue("Ghost:Plugins:Glassdoor:RegisterKeyedJobClient", true)
        };

        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

        if (pluginOptions.RegisterReadinessServices)
        {
            // Register plugin-specific services
            services.AddSingleton<GlassdoorPluginCapabilities>(sp => new GlassdoorPluginCapabilities
            {
                RequiresBrowser = true,
                RequiresProxy = false,
                SupportsJobs = true,
                SupportsSocial = false,
                SupportsNews = false
            });

            services.AddSingleton<IGlassdoorPluginReadinessCheck, GlassdoorPluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedJobClient)
        {
            // Register keyed IJobClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("glassdoor", (sp, _) =>
                sp.GetRequiredService<Ghost.Platform.Glassdoor.GlassdoorJobClient>());
        }
    }
}
