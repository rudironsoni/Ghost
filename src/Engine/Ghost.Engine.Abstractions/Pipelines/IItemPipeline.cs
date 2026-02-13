using Ghost.Engine.Abstractions.Engine;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Abstractions.Pipelines;

public interface IItemPipeline
{
    Task<ItemEnvelope> ProcessAsync(ItemEnvelope item, GhostEngineContext context, CancellationToken cancellationToken = default);
}
