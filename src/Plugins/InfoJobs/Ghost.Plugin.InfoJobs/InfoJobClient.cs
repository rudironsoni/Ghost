using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.InfoJobs;

public sealed class InfoJobClient : Ghost.IJobScraper
{
    private readonly Internal.InfoJobsApiClient _api;
    private readonly ILogger<InfoJobClient> _logger;

    public InfoJobClient(Internal.InfoJobsApiClient api, ILogger<InfoJobClient> logger)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(logger);
        _api = api;
        _logger = logger;
    }

    public string PlatformName => "InfoJobs";

    public Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, ct);
    }

    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        return await _api.GetJobDetailsAsync(jobId, ct).ConfigureAwait(false);
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
        => Task.FromResult(new JobApplication { JobId = jobId });

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<JobApplication>>(Array.Empty<JobApplication>());

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<JobListing>>(Array.Empty<JobListing>());
}
