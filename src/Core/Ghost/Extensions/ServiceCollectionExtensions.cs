using Microsoft.Extensions.DependencyInjection;
using Ghost.Abstractions;
using Ghost.Core.Services;
using Ghost.Contracts.Jobs;

namespace Ghost.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGhostAggregator(this IServiceCollection services)
    {
        services.AddScoped<AggregatedJobClient>();
        services.AddScoped<IJobClient>(sp => sp.GetRequiredService<AggregatedJobClient>());
        services.AddScoped<IJobScraper>(sp => sp.GetRequiredService<AggregatedJobClient>());
        return services;
    }
}
