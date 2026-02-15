using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost;

public interface IProxySource
{
    public Task<IEnumerable<ProxyInfo>> FetchProxiesAsync(CancellationToken ct);
}
