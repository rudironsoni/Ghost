using System;
using Ghost.Abstractions;

namespace Ghost.Proxy;

public sealed class StaticProxyProvider : IProxyProvider
{
    public static StaticProxyProvider Empty { get; } = new(Array.Empty<ProxyInfo>());
    private readonly List<ProxyInfo> _proxies;
    private int _index;

    public StaticProxyProvider(IEnumerable<ProxyInfo> proxies)
    {
        _proxies = proxies.ToList();
    }

    public Task<ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken token = default)
    {
        if (_proxies.Count == 0)
        {
            return Task.FromResult<ProxyInfo?>(null);
        }

        int index = Interlocked.Increment(ref _index) % _proxies.Count;
        return Task.FromResult<ProxyInfo?>(_proxies[index]);
    }
}
