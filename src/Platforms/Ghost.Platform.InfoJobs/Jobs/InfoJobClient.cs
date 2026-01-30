using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.InfoJobs.Jobs;

public sealed class InfoJobClient : Ghost.Abstractions.IJobScraper
{
    private readonly Internal.InfoJobsApiClient _api;
    private readonly ILogger<InfoJobClient> _logger;

    public InfoJobClient(Internal.InfoJobsApiClient api, ILogger<InfoJobClient> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public string PlatformName => "InfoJobs";

    public Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, ct);
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
}