namespace Ghost.Sdk.Throttling;

/// <summary>
/// Interface for adaptive rate limiting based on server response times.
/// </summary>
/// <remarks>
/// Implementations of this interface automatically adjust download delays based on
/// observed server latency, balancing crawl speed against server load. This is essential
/// for responsible web scraping that avoids overwhelming target servers.
/// </remarks>
public interface IAutoThrottle
{
    /// <summary>
    /// Gets the current adaptive delay that should be applied between requests.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// The delay in seconds that should be waited before the next request.
    /// </returns>
    /// <remarks>
    /// The returned delay is calculated based on recent server response latencies
    /// and will be bounded by the configured minimum and maximum delay values.
    /// </remarks>
    Task<double> GetDelayAsync(CancellationToken ct = default);

    /// <summary>
    /// Records a server response latency measurement for adaptive adjustment.
    /// </summary>
    /// <param name="latency">The measured response latency.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <remarks>
    /// This method should be called after each request completes to feed the
    /// adaptive algorithm. The throttle will automatically adjust the delay based
    /// on a rolling window of latency measurements.
    /// </remarks>
    Task RecordLatencyAsync(TimeSpan latency, CancellationToken ct = default);
}
