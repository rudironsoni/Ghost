using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed.Internal;

namespace Ghost.Platform.Indeed;

public class IndeedJobClient : IJobClient
{
    private readonly IndeedApiClient _api;

    public IndeedJobClient(IndeedApiClient api)
    {
        _api = api;
    }

    public string PlatformName => "Indeed";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var list = new List<JobListing>();
        await foreach (var root in _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, criteria.MaxResults))
        {
            list.AddRange(IndeedJobParser.ParseJobs(root));
        }
        return list;
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default) =>
        Task.FromResult(new JobListing { Id = jobId });

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) =>
        Task.FromResult(new JobApplication());

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<JobApplication>)new List<JobApplication>());

    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) =>
        Task.FromResult((IReadOnlyList<JobListing>)new List<JobListing>());
}
