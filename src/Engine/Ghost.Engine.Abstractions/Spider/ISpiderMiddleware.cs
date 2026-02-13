using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Abstractions.Spider;

public interface ISpiderMiddleware
{
    Task<SpiderOutput> InvokeAsync(
        GhostResponse response,
        GhostEngineContext context,
        Func<GhostResponse, GhostEngineContext, CancellationToken, Task<SpiderOutput>> nextStep,
        CancellationToken cancellationToken = default);
}
