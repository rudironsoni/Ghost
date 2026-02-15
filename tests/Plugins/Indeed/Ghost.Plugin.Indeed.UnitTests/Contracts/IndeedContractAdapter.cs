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
    private readonly IJobClient _jobClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="IndeedContractAdapter"/> class.
    /// </summary>
    public IndeedContractAdapter(IJobClient jobClient)
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

        // Indeed uses offset-based pagination
        // We'll simulate this by making multiple searches with different criteria
        for (int page = 0; page < maxPages; page++)
        {
            var pageCriteria = new JobSearchCriteria
            {
                Query = criteria.Query,
                Location = criteria.Location,
                // Indeed-specific pagination would go here
                // For now, we'll just make the same request multiple times
                // In a real implementation, you'd use Indeed's pagination parameters
            };

            IReadOnlyList<JobListing> jobs = await _jobClient.SearchJobsAsync(pageCriteria, ct);

            if (jobs.Count == 0)
            {
                break; // No more results
            }

            allJobs.AddRange(jobs);

            // Check if we've seen all these jobs before (pagination exhausted)
            IEnumerable<string> newJobIds = jobs.Select(j => j.Id).Except(allJobs.Take(allJobs.Count - jobs.Count).Select(j => j.Id));
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
        // Make a request that may trigger rate limiting
        // The Indeed client should handle retries automatically
        return await _jobClient.SearchJobsAsync(criteria, ct);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<JobListing>> TestConsentFlowAsync(
        JobSearchCriteria criteria,
        CancellationToken ct = default)
    {
        // Make a request that may encounter consent dialogs
        // The Indeed client should handle consent flows automatically
        return await _jobClient.SearchJobsAsync(criteria, ct);
    }

    /// <inheritdoc />
    public async Task<(IReadOnlyList<JobListing> First, IReadOnlyList<JobListing> Second)> TestIdempotencyAsync(
        JobSearchCriteria criteria,
        CancellationToken ct = default)
    {
        IReadOnlyList<JobListing> first = await _jobClient.SearchJobsAsync(criteria, ct);
        IReadOnlyList<JobListing> second = await _jobClient.SearchJobsAsync(criteria, ct);
        return (first, second);
    }
}
