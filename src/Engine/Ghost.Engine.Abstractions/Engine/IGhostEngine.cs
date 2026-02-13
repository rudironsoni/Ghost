using Ghost.Engine.Abstractions.Spider;

namespace Ghost.Engine.Abstractions.Engine;

public interface IGhostEngine
{
    Task RunAsync(ISpider spider, GhostEngineContext context, CancellationToken cancellationToken = default);
}
