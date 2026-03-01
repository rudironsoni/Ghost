namespace Ghost.Sdk.Statistics;

/// <summary>
/// Contains execution statistics for a single spider instance.
/// </summary>
/// <remarks>
/// This class tracks various metrics about spider execution including request counts,
/// response times, error rates, and throughput. All numeric properties use long for
/// counts to support high-volume scraping scenarios.
/// </remarks>
public class SpiderStats
{
    /// <summary>
    /// Gets or sets the unique identifier of the spider.
    /// </summary>
    /// <value>The spider identifier.</value>
    public string SpiderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the total number of requests initiated by the spider.
    /// </summary>
    /// <value>The count of requests sent.</value>
    /// <remarks>
    /// This includes all requests, regardless of whether they succeeded or failed.
    /// </remarks>
    public long RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of responses received by the spider.
    /// </summary>
    /// <value>The count of responses received.</value>
    /// <remarks>
    /// This should match RequestCount in ideal scenarios. A lower value may indicate
    /// timeouts or network issues.
    /// </remarks>
    public long ResponseCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of errors encountered during execution.
    /// </summary>
    /// <value>The count of errors.</value>
    /// <remarks>
    /// Includes all exceptions caught during crawling, parsing, and processing.
    /// </remarks>
    public long ErrorCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of items successfully scraped.
    /// </summary>
    /// <value>The count of scraped items.</value>
    /// <remarks>
    /// Represents the productive output of the spider. Used to calculate success rate.
    /// </remarks>
    public long ItemCount { get; set; }

    /// <summary>
    /// Gets or sets the distribution of HTTP status codes received.
    /// </summary>
    /// <value>
    /// A concurrent dictionary mapping status codes to their occurrence counts.
    /// </value>
    /// <remarks>
    /// Useful for identifying server issues (5xx), client errors (4xx), or rate limiting (429).
    /// The dictionary is initialized as empty and populated as responses are recorded.
    /// Uses ConcurrentDictionary for thread-safe access from multiple threads.
    /// </remarks>
    public System.Collections.Concurrent.ConcurrentDictionary<int, long> StatusCodeDistribution { get; set; } = new();

    /// <summary>
    /// Gets or sets the total duration the spider has been running.
    /// </summary>
    /// <value>The elapsed time since the spider started.</value>
    /// <remarks>
    /// Updated on each response to reflect the current running time. Used to calculate
    /// throughput metrics.
    /// </remarks>
    public TimeSpan TotalDuration { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when the spider started execution.
    /// </summary>
    /// <value>The start time in UTC.</value>
    /// <remarks>
    /// Set when the first request is recorded. Used to calculate TotalDuration.
    /// </remarks>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets or sets the average response time across all requests.
    /// </summary>
    /// <value>The average latency in milliseconds.</value>
    /// <remarks>
    /// Calculated from all recorded response latencies. Useful for detecting performance
    /// degradation or comparing spider efficiency.
    /// </remarks>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets the throughput rate in requests per second.
    /// </summary>
    /// <value>
    /// The number of requests per second, or 0 if no time has elapsed.
    /// </value>
    /// <remarks>
    /// Calculated as RequestCount / TotalDuration.TotalSeconds. Returns 0 if the spider
    /// has not been running long enough to calculate a meaningful rate.
    /// </remarks>
    public double RequestsPerSecond => TotalDuration.TotalSeconds > 0
        ? RequestCount / TotalDuration.TotalSeconds
        : 0;
}
