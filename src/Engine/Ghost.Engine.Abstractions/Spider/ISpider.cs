using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Abstractions.Spider;

public interface ISpider
{
    public string Name { get; }

    public IAsyncEnumerable<GhostRequest> StartRequestsAsync(GhostEngineContext context, CancellationToken cancellationToken = default);

    public Task<SpiderOutput> ParseAsync(GhostResponse response, GhostEngineContext context, CancellationToken cancellationToken = default);
}
