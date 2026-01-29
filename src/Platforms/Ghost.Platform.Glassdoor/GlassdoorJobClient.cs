using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Glassdoor;

public sealed class GlassdoorJobClient : IJobClient
{
    private readonly Internal.GlassdoorApiClient _api;
    private readonly GlassdoorOptions _options;

    public GlassdoorJobClient(Internal.GlassdoorApiClient api, IOptions<GlassdoorOptions> options)
    {
        _api = api;
        _options = options.Value;
    }

    public string PlatformName => "Glassdoor";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var payload = await _api.SearchAsync(criteria.Keywords ?? string.Empty, criteria.Location, null, ct);
        return Internal.GlassdoorJobParser.ParseSearchResponse(payload);
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default) => Task.FromResult(new JobListing { Id = jobId });
    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) => Task.FromException<JobApplication>(new NotImplementedException());
    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobApplication>)Array.Empty<JobApplication>());
    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobListing>)Array.Empty<JobListing>());
}
