using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Ghost.Kernel;

#pragma warning disable CA1711
public interface IGenericDeadLetterQueue
{
    public Task EnqueueAsync<T>(T item, string reason, Exception? exception = null, CancellationToken cancellationToken = default);
    public Task<List<DeadLetterItem>> PeekAsync(int count = 10, CancellationToken cancellationToken = default);
    public Task<List<DeadLetterItem>> DequeueAsync(int count = 10, CancellationToken cancellationToken = default);
    public Task<int> GetCountAsync(CancellationToken cancellationToken = default);
    public Task ClearAsync(CancellationToken cancellationToken = default);
}

public class DeadLetterItem
{
    public Guid Id { get; init; }
    public DateTime EnqueuedAt { get; init; }
    public string Reason { get; init; } = string.Empty;
    public string? ExceptionMessage { get; init; }
    public string ExceptionType { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public int RetryCount { get; init; }
}

#pragma warning disable CA1711
public class InMemoryDeadLetterQueue : IGenericDeadLetterQueue
{
    private static readonly Action<ILogger, Guid, string, string, Exception?> _itemEnqueued = LoggerMessage.Define<Guid, string, string>(
        LogLevel.Warning,
        new EventId(1, "ItemEnqueued"),
        "Item enqueued to DLQ: {Id}, Reason: {Reason}, Type: {Type}");

    private static readonly Action<ILogger, int, Exception?> _itemsDequeued = LoggerMessage.Define<int>(
        LogLevel.Information,
        new EventId(2, "ItemsDequeued"),
        "Dequeued {Count} items from DLQ");

    private static readonly Action<ILogger, int, Exception?> _queueCleared = LoggerMessage.Define<int>(
        LogLevel.Information,
        new EventId(3, "QueueCleared"),
        "Cleared {Count} items from DLQ");

    private readonly ConcurrentQueue<DeadLetterItem> _queue = new();
    private readonly ILogger<InMemoryDeadLetterQueue> _logger;
    private readonly object _lock = new();

    public InMemoryDeadLetterQueue(ILogger<InMemoryDeadLetterQueue> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task EnqueueAsync<T>(T item, string reason, Exception? exception = null, CancellationToken cancellationToken = default)
    {
        string safeReason = reason ?? "No reason provided";

        var deadLetterItem = new DeadLetterItem
        {
            Id = Guid.NewGuid(),
            EnqueuedAt = DateTime.UtcNow,
            Reason = safeReason,
            ExceptionMessage = exception?.Message,
            ExceptionType = exception?.GetType().Name ?? string.Empty,
            ContentType = typeof(T).Name,
            Content = JsonSerializer.Serialize(item),
            RetryCount = 0
        };

        _queue.Enqueue(deadLetterItem);
        _itemEnqueued(_logger, deadLetterItem.Id, safeReason, typeof(T).Name, null);

        return Task.CompletedTask;
    }

    public Task<List<DeadLetterItem>> PeekAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_lock)
        {
            return Task.FromResult(_queue.Take(Math.Min(count, _queue.Count)).ToList());
        }
    }

    public Task<List<DeadLetterItem>> DequeueAsync(int count, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var items = new List<DeadLetterItem>();

        lock (_lock)
        {
            for (int i = 0; i < count && _queue.TryDequeue(out DeadLetterItem? item); i++)
            {
                items.Add(item);
            }
        }

        _itemsDequeued(_logger, items.Count, null);
        return Task.FromResult(items);
    }

    public Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_queue.Count);
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        int initialCount = _queue.Count;

        while (_queue.TryDequeue(out _))
        {
        }

        _queueCleared(_logger, initialCount, null);
        return Task.CompletedTask;
    }
}
