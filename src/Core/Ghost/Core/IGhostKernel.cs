using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Core;

public interface IGhostKernel
{
    Task<IBrowserSession> NewSessionAsync(SessionOptions? options = null, CancellationToken ct = default);
}
