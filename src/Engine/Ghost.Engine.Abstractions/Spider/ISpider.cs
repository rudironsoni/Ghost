using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Abstractions.Spider;

public interface ISpider
{
    string Name { get; }

    IAsyncEnumerable<GhostRequest> StartRequestsAsync(GhostEngineContext context, CancellationToken cancellationToken = default);

    Task<SpiderOutput> ParseAsync(GhostResponse response, GhostEngineContext context, CancellationToken cancellationToken = default);
}
