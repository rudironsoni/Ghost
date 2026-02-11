using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed;
using Ghost.Testing.Contracts;

namespace Ghost.Platform.Indeed.Tests.Contracts;

/// <summary>
/// Adapter for Indeed provider contract testing.
/// </summary>
public sealed class IndeedContractAdapter : IProviderContractAdapter
{
    private readonly IndeedJobClient _jobClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndeedContractAdapter"/> class.
    /// </summary>
    public IndeedContractAdapter(IndeedJobClient jobClient)
    {
        _jobClient = jobClient;
    }

    /// <inheritdoc />
    public string PlatformName => "Indeed";

    /// <inheritdoc />
    public Task<IReadOnlyList<JobListing>> GetJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        return _jobClient.SearchJobsAsync(criteria, ct);
    }

    /// <inheritdoc />
    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        return _jobClient.GetJobDetailsAsync(jobId, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobListing>> SearchWithPaginationAsync(
        JobSearchCriteria criteria,
        int maxPages = 10,
        CancellationToken ct = default)
    {
        var allJobs = new List<JobListing>();

        // Indeed uses cursor-based pagination
        // We'll simulate this by making multiple searches
        for (int page = 0; page < maxPages; page++)
        {
            var pageCriteria = new JobSearchCriteria
            {
                Query = criteria.Query,
                Location = criteria.Location,
                // Indeed-specific pagination would go here
            };

            var jobs = await _jobClient.SearchJobsAsync(pageCriteria, ct);

            if (jobs.Count == 0)
            {
                break; // No more results
            }

            allJobs.AddRange(jobs);

            // Check if we've seen all these jobs before (pagination exhausted)
            var newJobIds = jobs.Select(j => j.Id).Except(allJobs.Take(allJobs.Count - jobs.Count).Select(j => j.Id));
            if (!newJobIds.Any())
            {
                break;
            }
        }

        return allJobs;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobListing>> TestRetryBehaviorAsync(
        JobSearchCriteria criteria,
        CancellationToken ct = default)
    {
        return await _jobClient.SearchJobsAsync(criteria, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobListing>> TestConsentFlowAsync(
        JobSearchCriteria criteria,
        CancellationToken ct = default)
    {
        return await _jobClient.SearchJobsAsync(criteria, ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<JobListing> First, IReadOnlyList<JobListing> Second)> TestIdempotencyAsync(
        JobSearchCriteria criteria,
        CancellationToken ct = default)
    {
        var first = await _jobClient.SearchJobsAsync(criteria, ct);
        var second = await _jobClient.SearchJobsAsync(criteria, ct);
        return (first, second);
    }
}
