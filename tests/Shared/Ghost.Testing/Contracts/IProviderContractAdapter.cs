using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;

namespace Ghost.Testing.Contracts;

/// <summary>
/// Adapter interface for provider contract testing.
/// Each provider implements this to expose its functionality for contract validation.
/// </summary>
public interface IProviderContractAdapter
{
    /// <summary>
    /// Platform name (e.g., "Indeed", "Google", "LinkedIn").
    /// </summary>
    public string PlatformName { get; }

    /// <summary>
    /// Gets jobs using the provider's search functionality.
    /// </summary>
    public Task<IReadOnlyList<JobListing>> GetJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>
    /// Gets detailed information for a specific job.
    /// </summary>
    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Searches for jobs with pagination support.
    /// Returns all pages until exhausted or maxPages reached.
    /// </summary>
    public Task<IReadOnlyList<JobListing>> SearchWithPaginationAsync(
        JobSearchCriteria criteria,
        int maxPages = 10,
        CancellationToken ct = default);

    /// <summary>
    /// Tests retry behavior by simulating a failure scenario.
    /// </summary>
    public Task<IReadOnlyList<JobListing>> TestRetryBehaviorAsync(
        JobSearchCriteria criteria,
        CancellationToken ct = default);

    /// <summary>
    /// Tests consent flow handling.
    /// </summary>
    public Task<IReadOnlyList<JobListing>> TestConsentFlowAsync(
        JobSearchCriteria criteria,
        CancellationToken ct = default);

    /// <summary>
    /// Tests idempotent extraction - same input should produce same output.
    /// </summary>
    public Task<(IReadOnlyList<JobListing> First, IReadOnlyList<JobListing> Second)> TestIdempotencyAsync(
        JobSearchCriteria criteria,
        CancellationToken ct = default);
}
