
namespace Ghost.Proxy;

public interface IProxyManager
{
    public Task<ProxyInfo?> GetProxyAsync(string? countryCode = null, CancellationToken ct = default);
    public Task<IReadOnlyList<ProxyHealthStatus>> GetHealthStatusAsync(CancellationToken ct = default);
    public Task<bool> HealthCheckAsync(string providerName, CancellationToken ct = default);
    public Task RegisterProviderAsync(IProxyProvider provider, ProxyProviderConfig config, CancellationToken ct = default);
    public Task UnregisterProviderAsync(string providerName, CancellationToken ct = default);
    public Task<IReadOnlyList<string>> GetAvailableCountriesAsync(CancellationToken ct = default);
}
