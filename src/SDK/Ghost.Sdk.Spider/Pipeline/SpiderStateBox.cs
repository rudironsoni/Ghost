using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Ghost.Sdk.Spider.Pipeline;

/// <summary>
/// Provides a thread-safe container for complex spider state that outlives individual requests.
/// This class serves as an escape hatch for state that cannot be efficiently stored in the
/// value-type PipelineContext structure.
/// </summary>
/// <remarks>
/// <para>
/// SpiderStateBox is designed for scenarios where:
/// - State needs to be shared across multiple pipeline executions
/// - Complex objects or collections need to be maintained
/// - Reference semantics are required for state management
/// - Thread-safe operations are necessary
/// </para>
/// <para>
/// For simple counters and metrics, prefer using Interlocked operations directly
/// on fields in this class for optimal performance. The Properties dictionary
/// should be used for dynamic or plugin-provided state only.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// var stateBox = new SpiderStateBox();
/// stateBox.Properties["visited_urls"] = new HashSet&lt;string&gt;();
/// stateBox.IncrementRequestCount();
/// </code>
/// </example>
public sealed class SpiderStateBox
{
    private long _requestCount;
    private long _successCount;
    private long _errorCount;
    private long _retryCount;

    /// <summary>
    /// Gets the thread-safe dictionary for storing arbitrary state properties.
    /// Use this for complex state that needs to be shared across pipeline executions.
    /// </summary>
    /// <remarks>
    /// This dictionary is thread-safe for concurrent reads and writes.
    /// For simple counters, prefer using the dedicated counter methods
    /// (IncrementRequestCount, etc.) which use lock-free Interlocked operations.
    /// </remarks>
    public ConcurrentDictionary<string, object> Properties { get; } = new();

    /// <summary>
    /// Gets the total number of requests processed by this spider.
    /// This count is maintained using lock-free atomic operations.
    /// </summary>
    public long RequestCount => Interlocked.Read(ref _requestCount);

    /// <summary>
    /// Gets the total number of successful requests processed by this spider.
    /// This count is maintained using lock-free atomic operations.
    /// </summary>
    public long SuccessCount => Interlocked.Read(ref _successCount);

    /// <summary>
    /// Gets the total number of failed requests processed by this spider.
    /// This count is maintained using lock-free atomic operations.
    /// </summary>
    public long ErrorCount => Interlocked.Read(ref _errorCount);

    /// <summary>
    /// Gets the total number of retry attempts made by this spider.
    /// This count is maintained using lock-free atomic operations.
    /// </summary>
    public long RetryCount => Interlocked.Read(ref _retryCount);

    /// <summary>
    /// Atomically increments the request count and returns the new value.
    /// </summary>
    /// <returns>The incremented request count.</returns>
    public long IncrementRequestCount() => Interlocked.Increment(ref _requestCount);

    /// <summary>
    /// Atomically increments the success count and returns the new value.
    /// </summary>
    /// <returns>The incremented success count.</returns>
    public long IncrementSuccessCount() => Interlocked.Increment(ref _successCount);

    /// <summary>
    /// Atomically increments the error count and returns the new value.
    /// </summary>
    /// <returns>The incremented error count.</returns>
    public long IncrementErrorCount() => Interlocked.Increment(ref _errorCount);

    /// <summary>
    /// Atomically increments the retry count and returns the new value.
    /// </summary>
    /// <returns>The incremented retry count.</returns>
    public long IncrementRetryCount() => Interlocked.Increment(ref _retryCount);

    /// <summary>
    /// Attempts to retrieve a value from the properties dictionary with the specified key.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="value">
    /// When this method returns, contains the value if found and of the correct type;
    /// otherwise, the default value for type T.
    /// </param>
    /// <returns>
    /// True if the key was found and the value is of the expected type; otherwise, false.
    /// </returns>
    public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
    {
        if (Properties.TryGetValue(key, out var obj) && obj is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Gets a value from the properties dictionary, or returns a default value if not found.
    /// </summary>
    /// <typeparam name="T">The expected type of the value.</typeparam>
    /// <param name="key">The key of the value to retrieve.</param>
    /// <param name="defaultValue">The default value to return if the key is not found.</param>
    /// <returns>The value if found and of the correct type; otherwise, the default value.</returns>
    public T GetValueOrDefault<T>(string key, T defaultValue = default!) where T : notnull
    {
        return TryGetValue<T>(key, out var value) ? value : defaultValue;
    }

    /// <summary>
    /// Sets a value in the properties dictionary, creating or updating the key.
    /// </summary>
    /// <typeparam name="T">The type of the value to store.</typeparam>
    /// <param name="key">The key of the value to set.</param>
    /// <param name="value">The value to store.</param>
    public void SetValue<T>(string key, T value) where T : notnull
    {
        Properties[key] = value;
    }

    /// <summary>
    /// Resets all counters to zero.
    /// This operation is not atomic across all counters.
    /// </summary>
    public void ResetCounters()
    {
        Interlocked.Exchange(ref _requestCount, 0);
        Interlocked.Exchange(ref _successCount, 0);
        Interlocked.Exchange(ref _errorCount, 0);
        Interlocked.Exchange(ref _retryCount, 0);
    }

    /// <summary>
    /// Clears all properties from the state box.
    /// </summary>
    public void ClearProperties()
    {
        Properties.Clear();
    }

    /// <summary>
    /// Completely resets the state box, clearing all properties and counters.
    /// </summary>
    public void Reset()
    {
        ResetCounters();
        ClearProperties();
    }
}
