using System.Collections.Concurrent;
using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Engine.Queue;

/// <summary>
/// In-memory implementation of request queue.
/// </summary>
/// <remarks>
/// This queue stores requests in memory using concurrent collections for thread-safety.
/// It supports priority-based ordering and automatic deduplication.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "InMemoryRequestQueue is implementing a queue data structure and the name is appropriate")]
public class InMemoryRequestQueue : IRequestQueue
{
    private readonly PriorityQueue<Request, int> _queue;
    private readonly ConcurrentDictionary<string, bool> _seenUrls;
    private readonly object _lock = new();

    /// <inheritdoc/>
    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsEmpty
    {
        get
        {
            lock (_lock)
            {
                return _queue.Count == 0;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryRequestQueue"/> class.
    /// </summary>
    public InMemoryRequestQueue()
    {
        _queue = new PriorityQueue<Request, int>(Comparer<int>.Create((a, b) => b.CompareTo(a))); // Higher priority first
        _seenUrls = new ConcurrentDictionary<string, bool>();
    }

    /// <inheritdoc/>
    public Task EnqueueAsync(Request request, int priority = 0, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Add to seen URLs for deduplication
        if (!_seenUrls.TryAdd(request.Url, true))
        {
            // URL already seen, skip
            return Task.CompletedTask;
        }

        lock (_lock)
        {
            _queue.Enqueue(request, priority);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Request?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                return Task.FromResult<Request?>(null);
            }

            Request request = _queue.Dequeue();
            return Task.FromResult<Request?>(request);
        }
    }

    /// <inheritdoc/>
    public Task<Request?> PeekAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_queue.Count == 0)
            {
                return Task.FromResult<Request?>(null);
            }

            Request request = _queue.Peek();
            return Task.FromResult<Request?>(request);
        }
    }

    /// <inheritdoc/>
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            _queue.Clear();
        }

        _seenUrls.Clear();

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<bool> ContainsAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(url);

        bool contains = _seenUrls.ContainsKey(url);
        return Task.FromResult(contains);
    }
}
