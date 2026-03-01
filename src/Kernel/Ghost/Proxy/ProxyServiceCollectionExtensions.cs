using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Ghost.Proxy;

public static class ProxyServiceCollectionExtensions
{
    public static IServiceCollection AddProxyManager(
        this IServiceCollection services,
        Action<ProxyConfiguration> configureOptions)
    {
        services.Configure(configureOptions);
        services.AddSingleton<IProxyManager, ProxyManager>();
        return services;
    }
}
