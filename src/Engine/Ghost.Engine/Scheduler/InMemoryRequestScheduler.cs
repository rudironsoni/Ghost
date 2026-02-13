using System.Collections.Concurrent;
using Ghost.Engine.Abstractions.Scheduler;
using Ghost.Engine.Abstractions.Transport;

namespace Ghost.Engine.Scheduler;

/// <summary>
/// In-memory priority-aware request scheduler with optional deduplication.
/// Lower priority number = higher priority.
/// </summary>
public sealed class InMemoryRequestScheduler : IRequestScheduler
{
    private readonly ConcurrentDictionary<int, ConcurrentQueue<GhostRequest>> _priorityQueues = new();
    private readonly InMemoryRequestSchedulerOptions _options;
    private int _minPriority = int.MaxValue;
    private int _maxPriority = int.MinValue;
    private int _totalPending;

    public InMemoryRequestScheduler(InMemoryRequestSchedulerOptions? options = null)
    {
        _options = options ?? new InMemoryRequestSchedulerOptions();
    }

    public async ValueTask EnqueueAsync(GhostRequest request, int priority = 0, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Check deduplication
        if (_options.ShouldSkip != null && _options.ShouldSkip(request))
        {
            return;
        }

        var queue = _priorityQueues.GetOrAdd(priority, _ => new ConcurrentQueue<GhostRequest>());
        queue.Enqueue(request);

        Interlocked.Increment(ref _totalPending);

        // Update priority bounds
        if (priority < _minPriority)
        {
            Interlocked.Exchange(ref _minPriority, priority);
        }
        if (priority > _maxPriority)
        {
            Interlocked.Exchange(ref _maxPriority, priority);
        }

        await ValueTask.CompletedTask;
    }

    public async ValueTask<GhostRequest?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        // Try to dequeue from highest priority (lowest number) to lowest
        for (int priority = Volatile.Read(ref _minPriority); priority <= Volatile.Read(ref _maxPriority); priority++)
        {
            if (_priorityQueues.TryGetValue(priority, out var queue))
            {
                if (queue.TryDequeue(out var request))
                {
                    Interlocked.Decrement(ref _totalPending);
                    return request;
                }
            }
        }

        return await ValueTask.FromResult<GhostRequest?>(null);
    }

    public async ValueTask<int> CountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await ValueTask.FromResult(Volatile.Read(ref _totalPending));
    }
}
