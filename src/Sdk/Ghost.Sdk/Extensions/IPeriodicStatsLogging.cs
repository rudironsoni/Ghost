namespace Ghost.Sdk.Extensions;

/// <summary>
/// Interface for logging spider statistics periodically during execution.
/// </summary>
/// <remarks>
/// This extension provides automated periodic logging of spider metrics including
/// request counts, response times, error rates, and throughput. Useful for monitoring
/// long-running spiders and identifying performance issues in real-time.
/// </remarks>
public interface IPeriodicStatsLogging
{
    /// <summary>
    /// Gets or sets the interval between stat logging outputs.
    /// </summary>
    /// <value>The time interval between log outputs. Default is 30 seconds.</value>
    /// <remarks>
    /// Can be adjusted dynamically during execution. Changes take effect after
    /// the current interval completes.
    /// </remarks>
    public TimeSpan Interval { get; set; }

    /// <summary>
    /// Starts periodic logging for the specified spider.
    /// </summary>
    /// <param name="spiderId">The unique identifier of the spider to monitor.</param>
    /// <exception cref="ArgumentNullException">Thrown when spiderId is null.</exception>
    /// <remarks>
    /// Begins a timer that periodically queries the stats collector and logs
    /// the current statistics. Safe to call multiple times - subsequent calls
    /// restart the timer with the current spider ID.
    /// </remarks>
    public void StartLogging(string spiderId);

    /// <summary>
    /// Stops periodic logging and releases timer resources.
    /// </summary>
    /// <remarks>
    /// Safe to call multiple times. Does nothing if logging is not active.
    /// </remarks>
    public void StopLogging();
}
