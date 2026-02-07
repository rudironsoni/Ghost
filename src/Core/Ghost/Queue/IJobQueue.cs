namespace Ghost.Queue;

/// <summary>
/// Interface for job queue operations
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Enqueue a job with specified priority
    /// </summary>
    /// <param name="job">Job to enqueue</param>
    /// <param name="priority">Job priority (defaults to Normal)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Job ID</returns>
    Task<string> EnqueueAsync(Job job, int priority = 2, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeue the next available job for a worker
    /// </summary>
    /// <param name="workerId">Worker identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Next job or null if queue is empty</returns>
    Task<Job?> DequeueAsync(string workerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a job as completed successfully
    /// </summary>
    /// <param name="jobId">Job identifier</param>
    /// <param name="result">Job result</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CompleteAsync(string jobId, JobResult result, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark a job as failed and handle retry logic
    /// </summary>
    /// <param name="jobId">Job identifier</param>
    /// <param name="error">Exception that caused the failure</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task FailAsync(string jobId, Exception error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of pending jobs
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of pending jobs</returns>
    Task<int> GetPendingCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of active jobs
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of active jobs</returns>
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of completed jobs
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of completed jobs</returns>
    Task<int> GetCompletedCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of dead jobs (exhausted retries)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of dead jobs</returns>
    Task<int> GetDeadCountAsync(CancellationToken cancellationToken = default);
}
