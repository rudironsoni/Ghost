namespace Ghost.Sdk.Spider.Engine;

/// <summary>
/// Represents the execution context for a spider run.
/// </summary>
/// <remarks>
/// The execution context maintains state and statistics throughout the spider's lifecycle.
/// It's thread-safe and can be accessed concurrently from multiple workers.
/// </remarks>
public class ExecutionContext
{
    private int _requestsProcessed;
    private int _requestsSucceeded;
    private int _requestsFailed;
    private int _itemsExtracted;

    /// <summary>
    /// Gets the spider name.
    /// </summary>
    /// <value>The name of the executing spider.</value>
    public string SpiderName { get; }

    /// <summary>
    /// Gets the spider options.
    /// </summary>
    /// <value>The configuration options for this execution.</value>
    public SpiderOptions Options { get; }

    /// <summary>
    /// Gets the execution start time.
    /// </summary>
    /// <value>The UTC timestamp when execution started.</value>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets the number of requests processed.
    /// </summary>
    /// <value>The current request count.</value>
    public int RequestsProcessed => _requestsProcessed;

    /// <summary>
    /// Gets the number of successful requests.
    /// </summary>
    /// <value>The successful request count.</value>
    public int RequestsSucceeded => _requestsSucceeded;

    /// <summary>
    /// Gets the number of failed requests.
    /// </summary>
    /// <value>The failed request count.</value>
    public int RequestsFailed => _requestsFailed;

    /// <summary>
    /// Gets the number of items extracted.
    /// </summary>
    /// <value>The extracted item count.</value>
    public int ItemsExtracted => _itemsExtracted;

    /// <summary>
    /// Gets or sets a value indicating whether execution is paused.
    /// </summary>
    /// <value><c>true</c> if paused; otherwise, <c>false</c>.</value>
    public bool IsPaused { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether cancellation was requested.
    /// </summary>
    /// <value><c>true</c> if cancellation requested; otherwise, <c>false</c>.</value>
    public bool IsCancellationRequested { get; set; }

    /// <summary>
    /// Gets the shared state dictionary for custom data.
    /// </summary>
    /// <value>Thread-safe dictionary for storing execution state.</value>
    public System.Collections.Concurrent.ConcurrentDictionary<string, object> State { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExecutionContext"/> class.
    /// </summary>
    /// <param name="spiderName">The spider name.</param>
    /// <param name="options">The spider options.</param>
    public ExecutionContext(string spiderName, SpiderOptions options)
    {
        SpiderName = spiderName ?? throw new ArgumentNullException(nameof(spiderName));
        Options = options ?? throw new ArgumentNullException(nameof(options));
        StartedAt = DateTimeOffset.UtcNow;
        State = new System.Collections.Concurrent.ConcurrentDictionary<string, object>();
    }

    /// <summary>
    /// Increments the processed request counter.
    /// </summary>
    /// <returns>The new count after incrementing.</returns>
    public int IncrementRequestsProcessed()
    {
        return Interlocked.Increment(ref _requestsProcessed);
    }

    /// <summary>
    /// Increments the succeeded request counter.
    /// </summary>
    /// <returns>The new count after incrementing.</returns>
    public int IncrementRequestsSucceeded()
    {
        return Interlocked.Increment(ref _requestsSucceeded);
    }

    /// <summary>
    /// Increments the failed request counter.
    /// </summary>
    /// <returns>The new count after incrementing.</returns>
    public int IncrementRequestsFailed()
    {
        return Interlocked.Increment(ref _requestsFailed);
    }

    /// <summary>
    /// Increments the items extracted counter.
    /// </summary>
    /// <param name="count">Number of items to add. Defaults to 1.</param>
    /// <returns>The new count after incrementing.</returns>
    public int IncrementItemsExtracted(int count = 1)
    {
        return Interlocked.Add(ref _itemsExtracted, count);
    }

    /// <summary>
    /// Checks if the maximum request limit has been reached.
    /// </summary>
    /// <returns><c>true</c> if the limit is reached; otherwise, <c>false</c>.</returns>
    public bool IsRequestLimitReached()
    {
        return Options.MaxRequests.HasValue && _requestsProcessed >= Options.MaxRequests.Value;
    }

    /// <summary>
    /// Gets the current execution statistics.
    /// </summary>
    /// <returns>Dictionary of statistics.</returns>
    public Dictionary<string, object> GetStatistics()
    {
        TimeSpan elapsed = DateTimeOffset.UtcNow - StartedAt;
        double requestsPerSecond = elapsed.TotalSeconds > 0 ? _requestsProcessed / elapsed.TotalSeconds : 0;

        return new Dictionary<string, object>
        {
            ["RequestsProcessed"] = _requestsProcessed,
            ["RequestsSucceeded"] = _requestsSucceeded,
            ["RequestsFailed"] = _requestsFailed,
            ["ItemsExtracted"] = _itemsExtracted,
            ["ElapsedSeconds"] = elapsed.TotalSeconds,
            ["RequestsPerSecond"] = requestsPerSecond,
            ["SuccessRate"] = _requestsProcessed > 0 ? (double)_requestsSucceeded / _requestsProcessed : 0
        };
    }
}
