using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Abstractions.Scheduler;

public interface IRequestScheduler
{
    public ValueTask EnqueueAsync(GhostRequest request, int priority = 0, CancellationToken cancellationToken = default);

    public ValueTask<GhostRequest?> DequeueAsync(CancellationToken cancellationToken = default);

    public ValueTask<int> CountAsync(CancellationToken cancellationToken = default);
}
