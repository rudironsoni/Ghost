using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Abstractions.Scheduler;

public interface IRequestScheduler
{
    ValueTask EnqueueAsync(GhostRequest request, int priority = 0, CancellationToken cancellationToken = default);

    ValueTask<GhostRequest?> DequeueAsync(CancellationToken cancellationToken = default);

    ValueTask<int> CountAsync(CancellationToken cancellationToken = default);
}
