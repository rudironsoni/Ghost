using Ghost.Core.Caching;
using Ghost.Core.Configuration;
using Ghost.Core.Monitoring;
using Ghost.Resilience;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ghost.Core;

public static class ResilienceServiceCollectionExtensions
{
    public static IServiceCollection AddGhostResilience(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IRetryPolicy, RetryPolicy>();
        services.AddSingleton<IGenericDeadLetterQueue, InMemoryDeadLetterQueue>();
        services.AddSingleton<IMetricsCollector, MetricsCollector>();
        services.AddSingleton<INordVpnCredentialProvider, ConfigurationNordVpnCredentialProvider>();
        services.AddMemoryCache();
        services.AddSingleton<IScrapeCache>(sp => new MemoryFileHybridCache(
            sp.GetRequiredService<IMemoryCache>(),
            "/var/ghost/cache",
            sp.GetRequiredService<ILogger<MemoryFileHybridCache>>()));

        CircuitBreakerOptions circuitBreakerOptions = configuration.GetSection("Resilience:CircuitBreaker").Get<CircuitBreakerOptions>()
                                    ?? new CircuitBreakerOptions();
        services.AddSingleton<ICircuitBreaker>(new CircuitBreaker("LinkedIn", circuitBreakerOptions));
        services.AddSingleton<ICircuitBreaker>(new CircuitBreaker("Indeed", circuitBreakerOptions));
        services.AddSingleton<ICircuitBreaker>(new CircuitBreaker("Proxy", circuitBreakerOptions));

        return services;
    }
}
