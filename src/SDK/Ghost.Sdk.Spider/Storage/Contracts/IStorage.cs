namespace Ghost.Sdk.Spider.Storage.Contracts;

/// <summary>
/// Defines the contract for storage implementations that persist extracted data.
/// </summary>
/// <remarks>
/// Storage implementations handle the persistence of extracted data to various
/// destinations such as databases, search engines, files, or external APIs.
/// </remarks>
public interface IStorage
{
    /// <summary>
    /// Gets the name of this storage implementation.
    /// </summary>
    /// <value>A unique identifier for the storage type.</value>
    string Name { get; }

    /// <summary>
    /// Gets a value indicating whether this storage is available.
    /// </summary>
    /// <value><c>true</c> if the storage is properly configured and accessible; otherwise, <c>false</c>.</value>
    bool IsAvailable { get; }

    /// <summary>
    /// Initializes the storage connection and prepares it for use.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a single item.
    /// </summary>
    /// <typeparam name="T">The type of item to store.</typeparam>
    /// <param name="item">The item to store.</param>
    /// <param name="context">The storage context with metadata.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the storage result.</returns>
    Task<StorageResult> StoreAsync<T>(T item, StorageContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores multiple items in a batch.
    /// </summary>
    /// <typeparam name="T">The type of items to store.</typeparam>
    /// <param name="items">The items to store.</param>
    /// <param name="context">The storage context with metadata.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the storage result.</returns>
    Task<StorageResult> StoreBatchAsync<T>(
        IEnumerable<T> items,
        StorageContext context,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any pending writes to the storage destination.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the storage connection and releases resources.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task CloseAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the result of a storage operation.
/// </summary>
public class StorageResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the operation was successful.
    /// </summary>
    /// <value><c>true</c> if successful; otherwise, <c>false</c>.</value>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets or sets the number of items stored.
    /// </summary>
    /// <value>The item count.</value>
    public int ItemsStored { get; init; }

    /// <summary>
    /// Gets or sets error information if the operation failed.
    /// </summary>
    /// <value>The error message, or null if successful.</value>
    public string? Error { get; init; }

    /// <summary>
    /// Gets or sets the exception that occurred, if any.
    /// </summary>
    /// <value>The exception, or null if successful.</value>
    public Exception? Exception { get; init; }

    /// <summary>
    /// Gets or sets the duration of the operation.
    /// </summary>
    /// <value>The time elapsed.</value>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Gets or sets additional metadata about the operation.
    /// </summary>
    /// <value>Dictionary of metadata key-value pairs.</value>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Creates a successful storage result.
    /// </summary>
    /// <param name="itemsStored">Number of items stored.</param>
    /// <param name="duration">Operation duration.</param>
    /// <returns>A successful <see cref="StorageResult"/>.</returns>
    public static StorageResult CreateSuccess(int itemsStored, TimeSpan duration)
    {
        return new StorageResult
        {
            Success = true,
            ItemsStored = itemsStored,
            Duration = duration
        };
    }

    /// <summary>
    /// Creates a failed storage result.
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="duration">Operation duration.</param>
    /// <returns>A failed <see cref="StorageResult"/>.</returns>
    public static StorageResult CreateFailure(string error, Exception? exception, TimeSpan duration)
    {
        return new StorageResult
        {
            Success = false,
            ItemsStored = 0,
            Error = error,
            Exception = exception,
            Duration = duration
        };
    }
}
