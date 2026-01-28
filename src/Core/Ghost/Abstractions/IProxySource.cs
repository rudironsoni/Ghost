using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Abstractions;

public interface IProxySource
{
    Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct);
}
