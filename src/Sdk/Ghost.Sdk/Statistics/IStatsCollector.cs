namespace Ghost.Sdk.Statistics;

/// <summary>
/// Interface for collecting and reporting spider execution statistics.
/// </summary>
/// <remarks>
/// Implementations track various metrics during spider execution including
/// request/response counts, error rates, status code distribution, and performance metrics.
/// Used for monitoring spider health, debugging issues, and optimizing crawl performance.
/// </remarks>
public interface IStatsCollector
{
    /// <summary>
    /// Records that a request was initiated by the spider.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider making the request.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId is null.</exception>
    /// <remarks>
    /// This should be called before sending each request. The count is used to calculate
    /// throughput metrics like requests per second.
    /// </remarks>
    void RecordRequest(string spiderId);

    /// <summary>
    /// Records a received response with its status code and latency.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider that received the response.</param>
    /// <param name="statusCode">The HTTP status code of the response.</param>
    /// <param name="latency">The time taken to receive the response.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId is null.</exception>
    /// <remarks>
    /// This should be called after receiving each response. Records the status code distribution
    /// and updates average response time calculations. Thread-safe for concurrent spiders.
    /// </remarks>
    void RecordResponse(string spiderId, int statusCode, TimeSpan latency);

    /// <summary>
    /// Records an error that occurred during spider execution.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider that encountered the error.</param>
    /// <param name="ex">The exception that was thrown.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId or ex is null.</exception>
    /// <remarks>
    /// This should be called whenever an exception occurs during crawling, parsing, or
    /// processing. Used to track error rates and identify problem areas.
    /// </remarks>
    void RecordError(string spiderId, Exception ex);

    /// <summary>
    /// Records that an item was successfully scraped and processed.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider that scraped the item.</param>
    /// <param name="itemType">The type/category of the scraped item.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId or itemType is null.</exception>
    /// <remarks>
    /// This should be called after successfully extracting and validating each item.
    /// Used to track scraping productivity and success rates.
    /// </remarks>
    void RecordItem(string spiderId, string itemType);

    /// <summary>
    /// Gets the current statistics for a specific spider.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider.</param>
    /// <returns>
    /// A <see cref="SpiderStats"/> instance containing current metrics, or a new empty
    /// instance if the spider has not been tracked yet.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when spiderId is null.</exception>
    /// <remarks>
    /// Returns a snapshot of the current statistics. Safe to call while the spider is running.
    /// </remarks>
    SpiderStats GetStats(string spiderId);

    /// <summary>
    /// Gets the current statistics for all tracked spiders.
    /// </summary>
    /// <returns>
    /// A dictionary mapping spider IDs to their statistics. Returns an empty dictionary
    /// if no spiders have been tracked yet.
    /// </returns>
    /// <remarks>
    /// Returns a snapshot of all spider statistics. Useful for monitoring multiple
    /// concurrent spiders or generating aggregate reports.
    /// </remarks>
    Dictionary<string, SpiderStats> GetAllStats();
}
