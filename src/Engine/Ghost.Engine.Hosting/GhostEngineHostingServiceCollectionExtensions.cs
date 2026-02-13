using System.Linq;
using Ghost.Engine.Abstractions.Downloader;
using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Scheduler;
using Ghost.Engine.Downloader;
using Ghost.Engine.Engine;
using Ghost.Engine.Scheduler;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Ghost.Engine.Hosting;

public static class GhostEngineHostingServiceCollectionExtensions
{
    public static IServiceCollection AddGhostEngineHosting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<GhostEngineOptions>()
            .Bind(configuration.GetSection("Ghost:Engine"))
            .Validate(o => o.MaxInFlight > 0, "Ghost:Engine:MaxInFlight MUST be greater than 0")
            .Validate(o => o.MaxPendingItems > 0, "Ghost:Engine:MaxPendingItems MUST be greater than 0")
            .ValidateOnStart();

        services
            .AddOptions<InMemoryRequestSchedulerOptions>()
            .Bind(configuration.GetSection("Ghost:Engine:Scheduler"))
            .ValidateOnStart();

        services.TryAddSingleton<IRequestScheduler>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<InMemoryRequestSchedulerOptions>>().Value;
            return new InMemoryRequestScheduler(options);
        });

        services.TryAddSingleton<IDownloader, FakeDownloader>();

        services.TryAddSingleton<IGhostEngine>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GhostEngineOptions>>().Value;
            var scheduler = sp.GetRequiredService<IRequestScheduler>();
            var downloader = sp.GetRequiredService<IDownloader>();
            return new GhostEngine(
                options,
                scheduler,
                downloader,
                sp.GetServices<IDownloaderMiddleware>().ToList(),
                sp.GetServices<Ghost.Engine.Abstractions.Spider.ISpiderMiddleware>().ToList(),
                sp.GetServices<Ghost.Engine.Abstractions.Pipelines.IItemPipeline>().ToList());
        });

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, GhostEngineWarmupHostedService>());

        return services;
    }
}
