using Ghost.Abstractions;

namespace Ghost.Proxy;

public interface IProxyManager
{
    Task<ProxyInfo?> GetProxyAsync(string? countryCode = null, CancellationToken ct = default);
    Task<IReadOnlyList<ProxyHealthStatus>> GetHealthStatusAsync(CancellationToken ct = default);
    Task<bool> HealthCheckAsync(string providerName, CancellationToken ct = default);
    Task RegisterProviderAsync(IProxyProvider provider, ProxyProviderConfig config, CancellationToken ct = default);
    Task UnregisterProviderAsync(string providerName, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAvailableCountriesAsync(CancellationToken ct = default);
}
