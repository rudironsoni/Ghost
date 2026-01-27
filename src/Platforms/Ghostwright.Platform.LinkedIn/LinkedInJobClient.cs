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
    private readonly Internal.GuestJobSearch _guestSearch;

    public LinkedInJobClient(Ghostwright.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInJobClient> logger, Internal.GuestJobSearch guestSearch)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInJobClient>.Instance;
        _guestSearch = guestSearch ?? throw new ArgumentNullException(nameof(guestSearch));
    }

    // Back-compat constructor used by tests and callers that don't use DI for GuestJobSearch
    public LinkedInJobClient(Ghostwright.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInJobClient> logger)
        : this(session, options, logger, new Internal.GuestJobSearch(session, Microsoft.Extensions.Logging.Abstractions.NullLogger<Internal.GuestJobSearch>.Instance))
    {
    }

    public string PlatformName => "LinkedIn";

    public Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var list = new List<JobListing>();
        // Reuse the async enumerable search implementation
        var e = SearchJobsAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, criteria.MaxResults, ct);
        return Task.Run(async () =>
        {
            await foreach (var item in e.WithCancellation(ct))
            {
                list.Add(item);
            }
            return (IReadOnlyList<JobListing>)list;
        }, ct);
    }

    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        if (_options.ScrapingStrategy == JobScrapingStrategy.GuestApi)
        {
            var job = await _guestSearch.FetchJobDetailsAsync(jobId, ct).ConfigureAwait(false);
            if (job != null) return job;
            // fallthrough to browser if guest returns null
        }

        // fallback to browser logic
        return await GetJobDetailsBrowserAsync(jobId, ct).ConfigureAwait(false);
    }

    private async Task<JobListing> GetJobDetailsBrowserAsync(string jobId, CancellationToken ct = default)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            var url = $"{_options.BaseUrl}/jobs/view/{jobId}";
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            // attempt to parse JSON-LD from page content
            var html = await page.GetContentAsync(ct);
            var parsed = Internal.JsonLdParser.Parse(html ?? string.Empty, jobId, url);
            return parsed ?? new JobListing { Id = jobId, Url = url };
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        ArgumentNullException.ThrowIfNull(details);

        return ApplyInternalAsync(jobId, details, ct);
    }

    private async Task<JobApplication> ApplyInternalAsync(string jobId, ApplicationDetails details, CancellationToken ct)
    {
        var page = await _session.NewPageAsync(ct: ct);
        try
        {
            var url = $"{_options.BaseUrl}/jobs/view/{jobId}";
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            // Try to find a button that contains the text "Easy Apply"
            var buttons = await page.QuerySelectorAllAsync("button", ct: ct);
            IElement? applyBtn = null;
            foreach (var b in buttons)
            {
                try
                {
                    var txt = await b.GetTextContentAsync(ct) ?? string.Empty;
                    if (!string.IsNullOrEmpty(txt) && txt.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase))
                    {
                        applyBtn = b;
                        break;
                    }
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                }
            }

            if (applyBtn is null)
            {
                // No easy apply button found - indicate not applied
                return null!; // per spec: return null when button not found
            }

            await applyBtn.ClickAsync(ct: ct);
            // Wait a short moment for any potential modal or navigation
            try { await page.WaitForLoadStateAsync(ct: ct); } catch { }

            return new JobApplication
            {
                Id = Guid.NewGuid().ToString(),
                JobId = jobId,
                ApplicantId = details.ApplicantEmail ?? string.Empty,
                Status = "Applied",
                SubmittedAt = DateTimeOffset.UtcNow,
                Details = details
            };
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
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
        if (_options.ScrapingStrategy == JobScrapingStrategy.GuestApi)
        {
            // Use guest API to search then fetch details (do work outside iterator yields)
            var criteria = new Ghostwright.Contracts.Jobs.JobSearchCriteria { Query = keywords, Location = location, MaxResults = limit };
            var ids = await _guestSearch.SearchAsync(criteria, limit, ct);
            if (ids.Count == 0)
            {
                // no results
                yield break;
            }

            var results = new List<JobListing>();
            var returned = 0;
            foreach (var id in ids)
            {
                if (returned++ >= limit) break;
                ct.ThrowIfCancellationRequested();
                try
                {
                    var job = await _guestSearch.FetchJobDetailsAsync(id, ct);
                    if (job != null) results.Add(job);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                }
            }

            foreach (var job in results)
            {
                yield return job;
            }

            yield break;
        }

        if (_options.ScrapingStrategy == JobScrapingStrategy.Hybrid)
        {
            // Try guest API first (collect results before yielding)
            var criteria = new Ghostwright.Contracts.Jobs.JobSearchCriteria { Query = keywords, Location = location, MaxResults = limit };
            var ids = await _guestSearch.SearchAsync(criteria, limit, ct);
            if (ids.Count > 0)
            {
                var results = new List<JobListing>();
                var returned = 0;
                foreach (var id in ids)
                {
                    if (returned++ >= limit) break;
                    ct.ThrowIfCancellationRequested();
                    try
                    {
                        var job = await _guestSearch.FetchJobDetailsAsync(id, ct);
                        if (job != null) results.Add(job);
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                    }
                }

                foreach (var job in results)
                {
                    yield return job;
                }

                yield break;
            }

            // else fallthrough to browser
        }

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
                    var idEl = await n.QuerySelectorAsync("[data-id]", ct);
                    string id = idEl is not null ? await idEl.GetAttributeAsync("data-id", ct) ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString();

                    var titleEl = await n.QuerySelectorAsync(".job-card-list__title", ct);
                    string title = titleEl is not null ? await titleEl.GetTextContentAsync(ct) ?? string.Empty : string.Empty;

                    var companyEl = await n.QuerySelectorAsync(".job-card-container__company-name", ct);
                    string company = companyEl is not null ? await companyEl.GetTextContentAsync(ct) ?? string.Empty : string.Empty;

                    var locationEl = await n.QuerySelectorAsync(".job-card-container__metadata-item", ct);
                    string locationText = locationEl is not null ? await locationEl.GetTextContentAsync(ct) ?? string.Empty : string.Empty;

                    list.Add(new JobListing { Id = id, Title = title, Company = company, Location = locationText });
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
