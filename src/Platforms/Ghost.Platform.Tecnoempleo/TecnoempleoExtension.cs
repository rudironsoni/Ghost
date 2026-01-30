using Ghost.Contracts.Jobs;
using Ghost.Platform.Tecnoempleo.Jobs;
using Ghost.Platform.Tecnoempleo.Jobs.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Tecnoempleo;

public static class TecnoempleoExtension
{
    public static IServiceCollection AddTecnoempleo(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<TecnoempleoOptions>(configuration.GetSection("Tecnoempleo"));
        
        services.AddHttpClient<TecnoempleoApiClient>();
        
        services.TryAddScoped<TecnoempleoApiClient>();
        services.TryAddScoped<TecnoempleoClient>();
        
        services.TryAddScoped<IJobClient>(provider => provider.GetRequiredService<TecnoempleoClient>());
        
        return services;
    }

    public static IServiceCollection AddTecnoempleo(this IServiceCollection services, Action<TecnoempleoOptions> configureOptions)
    {
        services.Configure(configureOptions);
        
        services.AddHttpClient<TecnoempleoApiClient>();
        
        services.TryAddScoped<TecnoempleoApiClient>();
        services.TryAddScoped<TecnoempleoClient>();
        
        services.TryAddScoped<IJobClient>(provider => provider.GetRequiredService<TecnoempleoClient>());
        
        return services;
    }
}