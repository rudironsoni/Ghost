namespace Ghost.Sdk.Spider.Adapters.Contracts;

/// <summary>
/// Defines a pool for managing browser instances for JavaScript content extraction.
/// </summary>
/// <remarks>
/// Browser pools help manage the lifecycle and resource usage of browser instances
/// by reusing them across multiple requests. This improves performance and reduces
/// resource overhead compared to creating a new browser for each request.
/// </remarks>
public interface IBrowserPool : IAsyncDisposable
{
    /// <summary>
    /// Gets the maximum number of concurrent browser instances allowed.
    /// </summary>
    /// <value>The maximum pool size.</value>
    public int MaxPoolSize { get; }

    /// <summary>
    /// Gets the number of currently active browser instances.
    /// </summary>
    /// <value>The current number of active browsers.</value>
    public int ActiveCount { get; }

    /// <summary>
    /// Gets the number of idle browser instances available for use.
    /// </summary>
    /// <value>The count of idle browsers ready for acquisition.</value>
    public int IdleCount { get; }

    /// <summary>
    /// Acquires a browser session from the pool.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result contains
    /// an <see cref="IBrowserSession"/> instance that must be returned to the pool
    /// when no longer needed.
    /// </returns>
    /// <remarks>
    /// If no browser is available and the pool is at capacity, this method will wait
    /// until a browser becomes available or the cancellation token is triggered.
    /// </remarks>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the operation is canceled via <paramref name="cancellationToken"/>.
    /// </exception>
    public Task<IBrowserSession> AcquireAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a browser session to the pool for reuse.
    /// </summary>
    /// <param name="session">The browser session to return.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// After returning a session, it becomes available for other consumers to acquire.
    /// If the session is in an invalid state, it will be disposed and removed from the pool.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="session"/> is null.</exception>
    public Task ReleaseAsync(IBrowserSession session, CancellationToken cancellationToken = default);

    /// <summary>
    /// Initializes the browser pool and prepares browser instances.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous initialization operation.</returns>
    /// <remarks>
    /// This method should be called before using the pool. It may pre-create browser
    /// instances to reduce latency for the first requests.
    /// </remarks>
    public Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all idle browser instances from the pool.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This method disposes all idle browsers but does not affect active sessions.
    /// Use this to free resources when the pool is not expected to be used for a while.
    /// </remarks>
    public Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about the browser pool's performance and resource usage.
    /// </summary>
    /// <returns>A dictionary containing pool statistics.</returns>
    /// <remarks>
    /// Statistics may include:
    /// <list type="bullet">
    /// <item>TotalAcquisitions: Total number of browser acquisitions</item>
    /// <item>TotalReleases: Total number of browser releases</item>
    /// <item>AverageAcquisitionTime: Average time to acquire a browser</item>
    /// <item>BrowsersCreated: Total browsers created since pool initialization</item>
    /// <item>BrowsersDisposed: Total browsers disposed</item>
    /// </list>
    /// </remarks>
    public Dictionary<string, object> GetStatistics();
}
