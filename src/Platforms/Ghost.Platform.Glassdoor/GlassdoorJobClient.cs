using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Glassdoor;

public sealed class GlassdoorJobClient : Ghost.Abstractions.IJobScraper
{
    private readonly Internal.GlassdoorApiClient _api;
    private readonly Internal.GlassdoorBrowserClient _browserClient;
    private readonly GlassdoorOptions _options;
    private readonly ILogger<GlassdoorJobClient> _logger;

    private static readonly Action<ILogger, Exception?> s_logHttpFallback =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(GlassdoorJobClient)), "HTTP client returned no results, falling back to browser for Glassdoor");

    public GlassdoorJobClient(
        Internal.GlassdoorApiClient api,
        Internal.GlassdoorBrowserClient browserClient,
        IOptions<GlassdoorOptions> options,
        ILogger<GlassdoorJobClient> logger)
    {
        _api = api;
        _browserClient = browserClient;
        _options = options.Value;
        _logger = logger;
    }

    public string PlatformName => "Glassdoor";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var payload = await _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location, null, ct);
        var jobs = Internal.GlassdoorJobParser.ParseSearchResponse(payload);

        if (jobs.Count == 0 && _options.Enabled)
        {
            s_logHttpFallback(_logger, null);
            jobs = (List<JobListing>)await _browserClient.SearchAsync(criteria, criteria.MaxResults > 0 ? criteria.MaxResults : 20, ct);
        }

        return jobs;
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default) => Task.FromResult(new JobListing { Id = jobId, Source = "Glassdoor" });
    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) => Task.FromException<JobApplication>(new NotImplementedException());
    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobApplication>)Array.Empty<JobApplication>());
    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobListing>)Array.Empty<JobListing>());
}
