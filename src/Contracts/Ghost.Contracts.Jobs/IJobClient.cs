using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ghost.Contracts.Jobs;

/// <summary>
/// Abstraction for job platform clients (eg. LinkedIn, Indeed).
/// </summary>
public interface IJobClient
{
    /// <summary>
    /// Platform name.
    /// </summary>
    string PlatformName { get; }

    /// <summary>
    /// Searches for jobs using criteria.
    /// </summary>
    Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>
    /// Gets details for a specific job.
    /// </summary>
    Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Applies to a job with the provided application details.
    /// </summary>
    Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default);

    /// <summary>
    /// Gets applications for the authenticated account.
    /// </summary>
    Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default);

    /// <summary>
    /// Save a job for later viewing.
    /// </summary>
    Task SaveJobAsync(string jobId, CancellationToken ct = default);

    /// <summary>
    /// Gets saved jobs for the authenticated account.
    /// </summary>
    Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default);
}
