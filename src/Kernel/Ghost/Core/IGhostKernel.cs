using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Kernel;

public interface IGhostKernel
{
    public Task<IBrowserSession> NewSessionAsync(SessionOptions? options = null, CancellationToken ct = default);
}
