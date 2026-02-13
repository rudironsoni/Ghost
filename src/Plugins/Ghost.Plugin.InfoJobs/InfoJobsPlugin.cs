using Ghost.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ghost.Plugin.InfoJobs;

/// <summary>
/// InfoJobs plugin that wraps the platform extension with plugin metadata and capabilities.
/// </summary>
public sealed class InfoJobsPlugin : IExtension
{
    private readonly Ghost.Platform.InfoJobs.InfoJobsExtension _platformExtension;

    public InfoJobsPlugin()
    {
        _platformExtension = new Ghost.Platform.InfoJobs.InfoJobsExtension();
    }

    /// <inheritdoc />
    public string Name => "InfoJobs";

    /// <inheritdoc />
    public Version Version => new(1, 0, 0);

    /// <inheritdoc />
    public IReadOnlyList<Type> ProvidedServices => _platformExtension.ProvidedServices;

    /// <inheritdoc />
    public IReadOnlyList<Type> RequiredServices => _platformExtension.RequiredServices;

    /// <inheritdoc />
    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        var pluginOptions = new InfoJobsPluginOptions
        {
            UsePluginRuntime = configuration.GetValue("Ghost:Plugins:InfoJobs:UsePluginRuntime", true),
            RegisterReadinessServices = configuration.GetValue("Ghost:Plugins:InfoJobs:RegisterReadinessServices", true),
            RegisterKeyedJobClient = configuration.GetValue("Ghost:Plugins:InfoJobs:RegisterKeyedJobClient", true)
        };

        // Delegate to the platform extension for all core service registrations
        _platformExtension.ConfigureServices(services, configuration);

        if (pluginOptions.RegisterReadinessServices)
        {
            // Register plugin-specific services
            services.AddSingleton<InfoJobsPluginCapabilities>(sp => new InfoJobsPluginCapabilities
            {
                RequiresBrowser = true,
                RequiresProxy = false,
                SupportsJobs = true,
                SupportsSocial = false,
                SupportsNews = false
            });

            services.AddSingleton<IInfoJobsPluginReadinessCheck, InfoJobsPluginReadinessCheck>();
        }

        if (pluginOptions.UsePluginRuntime && pluginOptions.RegisterKeyedJobClient)
        {
            // Register keyed IJobClient mapping for worker compatibility.
            services.AddKeyedScoped<Ghost.Contracts.Jobs.IJobClient>("infojobs", (sp, _) =>
                sp.GetRequiredService<Ghost.Platform.InfoJobs.Jobs.InfoJobClient>());
        }
    }
}
