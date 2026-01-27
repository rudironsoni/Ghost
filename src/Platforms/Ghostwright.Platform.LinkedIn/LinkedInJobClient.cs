using Ghostwright.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghostwright.Platform.LinkedIn;

/// <summary>
/// Job search client for LinkedIn.
/// </summary>
public sealed class LinkedInJobClient : IJobClient
{
    private readonly Ghostwright.IBrowserSession _session;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInJobClient> _logger;

    public LinkedInJobClient(Ghostwright.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInJobClient> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInJobClient>.Instance;
    }

    public string PlatformName => "LinkedIn";

    public Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public async IAsyncEnumerable<JobListing> SearchJobsAsync(string keywords, string location, int limit = 25, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            var q = System.Uri.EscapeDataString(keywords);
            var loc = System.Uri.EscapeDataString(location);
            var url = $"{_options.BaseUrl}/jobs/search?keywords={q}&location={loc}";
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            var nodes = await page.QuerySelectorAllAsync(".jobs-search-results__list-item", ct: ct);
            var count = 0;
            var list = new List<JobListing>();
            foreach (var n in nodes)
            {
                if (count++ >= limit) break;
                try
                {
                    var id = await n.GetAttributeAsync("data-id", ct);
                    var titleEl = await n.QuerySelectorAsync(".job-card-list__title", ct);
                    var title = await titleEl?.GetTextContentAsync(ct) ?? string.Empty;
                    var companyEl = await n.QuerySelectorAsync(".job-card-container__company-name", ct);
                    var company = await companyEl?.GetTextContentAsync(ct) ?? string.Empty;
                    var locationEl = await n.QuerySelectorAsync(".job-card-container__metadata-item", ct);
                    var locationText = await locationEl?.GetTextContentAsync(ct) ?? string.Empty;
                    list.Add(new JobListing { Id = id ?? Guid.NewGuid().ToString(), Title = title, Company = company, Location = locationText });
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                }
            }
            
            foreach (var job in list)
            {
                yield return job;
            }
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }
}
