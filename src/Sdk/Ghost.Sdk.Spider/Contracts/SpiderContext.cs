namespace Ghost.Sdk.Spider.Contracts;

/// <summary>
/// Provides execution context information for spider contract validation.
/// </summary>
public class SpiderContext
{
    /// <summary>
    /// Gets or sets the unique identifier of the spider.
    /// </summary>
    public string SpiderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current execution state of the spider.
    /// </summary>
    public SpiderState State { get; set; }

    /// <summary>
    /// Gets or sets the total number of requests made by the spider.
    /// </summary>
    public int RequestCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of responses received by the spider.
    /// </summary>
    public int ResponseCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of items extracted by the spider.
    /// </summary>
    public int ItemCount { get; set; }

    /// <summary>
    /// Gets or sets the time when the spider started execution.
    /// </summary>
    public DateTimeOffset StartTime { get; set; }

    /// <summary>
    /// Gets the duration of spider execution from start time to now.
    /// </summary>
    public TimeSpan Duration => DateTimeOffset.UtcNow - StartTime;
}
