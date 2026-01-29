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
        services.AddScoped<Ghost.Contracts.Jobs.IJobClient>(sp => sp.GetRequiredService<AggregatedJobClient>());
        // AggregatedJobClient implements IJobClient; register as IJobScraper as well by wrapping
        services.AddScoped<Ghost.Abstractions.IJobScraper>(sp => sp.GetRequiredService<AggregatedJobClient>() as Ghost.Abstractions.IJobScraper ?? throw new InvalidOperationException("AggregatedJobClient is not an IJobScraper"));
        return services;
    }
}
