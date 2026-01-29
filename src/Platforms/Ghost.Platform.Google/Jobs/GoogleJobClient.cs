using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Google.Jobs;

public sealed class GoogleJobClient : Ghost.Abstractions.IJobScraper
{
    private readonly Internal.GoogleJobsApiClient _api;
    private readonly GoogleJobsOptions _options;
    private readonly ILogger<GoogleJobClient> _logger;

    public GoogleJobClient(Internal.GoogleJobsApiClient api, IOptions<GoogleJobsOptions> options, ILogger<GoogleJobClient> logger)
    {
        _api = api ?? throw new ArgumentNullException(nameof(api));
        _options = options?.Value ?? new GoogleJobsOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GoogleJobClient>.Instance;
    }

    public string PlatformName => "GoogleJobs";

    public Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty);
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
