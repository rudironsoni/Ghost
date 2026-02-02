using System.Collections.Generic;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Ghost.Resilience;

/// <summary>
/// Defines a dead letter queue for failed scrape jobs.
/// </summary>
[SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix", Justification = "Dead letter queue naming aligns with domain terminology.")]
public interface IDeadLetterQueue
{
    /// <summary>
    /// Enqueues a failed scrape job for later retry.
    /// </summary>
    /// <param name="job">The failed job to store.</param>
    Task EnqueueAsync(FailedScrapeJob job);

    /// <summary>
    /// Gets failed jobs newer than the provided window.
    /// </summary>
    /// <param name="since">The lookback window to include.</param>
    /// <returns>Matching failed jobs.</returns>
    Task<IReadOnlyList<FailedScrapeJob>> GetFailedJobsAsync(TimeSpan since);

    /// <summary>
    /// Gets failed jobs for a platform newer than the provided window.
    /// </summary>
    /// <param name="platform">The platform name to match.</param>
    /// <param name="since">The lookback window to include.</param>
    /// <returns>Matching failed jobs.</returns>
    Task<IReadOnlyList<FailedScrapeJob>> GetFailedJobsByPlatformAsync(string platform, TimeSpan since);

    /// <summary>
    /// Gets a failed job by id.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <returns>The failed job when found; otherwise null.</returns>
    Task<FailedScrapeJob?> GetJobAsync(string jobId);

    /// <summary>
    /// Retries a failed job by id.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    Task RetryAsync(string jobId);

    /// <summary>
    /// Retries all failed jobs newer than the provided window.
    /// </summary>
    /// <param name="since">The lookback window to include.</param>
    Task RetryAllAsync(TimeSpan since);

    /// <summary>
    /// Archives a failed job by id.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    Task ArchiveAsync(string jobId);

    /// <summary>
    /// Archives all jobs older than the provided threshold.
    /// </summary>
    /// <param name="olderThan">The age threshold for archiving.</param>
    Task ArchiveAllAsync(TimeSpan olderThan);

    /// <summary>
    /// Gets the current number of jobs in the active queue.
    /// </summary>
    /// <returns>Active queue depth.</returns>
    Task<int> GetQueueDepthAsync();
}
