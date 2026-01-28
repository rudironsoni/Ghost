using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Abstractions;

public record ProxyInfo(string Server, string? Username, string? Password);

public interface IProxyProvider
{
    Task<ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken token = default);
}
