using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Spider.Engine.Queue;

/// <summary>
/// Defines the contract for managing request queues.
/// </summary>
/// <remarks>
/// Request queues manage the pending requests in a spider, supporting
/// priority-based scheduling, deduplication, and persistence.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "IRequestQueue is an interface for a queue data structure and the name is appropriate")]
public interface IRequestQueue
{
    /// <summary>
    /// Gets the number of pending requests in the queue.
    /// </summary>
    /// <value>The count of pending requests.</value>
    public int Count { get; }

    /// <summary>
    /// Gets a value indicating whether the queue is empty.
    /// </summary>
    /// <value><c>true</c> if empty; otherwise, <c>false</c>.</value>
    public bool IsEmpty { get; }

    /// <summary>
    /// Enqueues a request.
    /// </summary>
    /// <param name="request">The request to enqueue.</param>
    /// <param name="priority">Optional priority (higher values = higher priority).</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task EnqueueAsync(Request request, int priority = 0, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next request.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The next request, or null if the queue is empty.</returns>
    public Task<Request?> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Peeks at the next request without removing it.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>The next request, or null if the queue is empty.</returns>
    public Task<Request?> PeekAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all requests from the queue.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a URL has already been queued or processed.
    /// </summary>
    /// <param name="url">The URL to check.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns><c>true</c> if the URL is a duplicate; otherwise, <c>false</c>.</returns>
    public Task<bool> ContainsAsync(string url, CancellationToken cancellationToken = default);
}
