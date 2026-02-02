using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ghost.Resilience;

namespace Ghost.Core;

public static class ResilienceServiceCollectionExtensions
{
    public static IServiceCollection AddGhostResilience(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IRetryPolicy, RetryPolicy>();
        services.AddSingleton<IDeadLetterQueue, InMemoryDeadLetterQueue>();

        var circuitBreakerOptions = configuration.GetSection("Resilience:CircuitBreaker").Get<CircuitBreakerOptions>()
                                    ?? new CircuitBreakerOptions();
        services.AddSingleton<ICircuitBreaker>(new CircuitBreaker("LinkedIn", circuitBreakerOptions));
        services.AddSingleton<ICircuitBreaker>(new CircuitBreaker("Indeed", circuitBreakerOptions));
        services.AddSingleton<ICircuitBreaker>(new CircuitBreaker("Proxy", circuitBreakerOptions));

        return services;
    }
}
