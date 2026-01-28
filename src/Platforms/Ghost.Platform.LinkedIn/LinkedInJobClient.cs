using Ghost.Contracts.Jobs;
using Ghost.Extensions;
using Ghost.Platform.LinkedIn.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;

namespace Ghost.Platform.LinkedIn;

/// <summary>
/// Job search client for LinkedIn.
/// </summary>
    public sealed class LinkedInJobClient : IJobClient
    {
        private static readonly Action<ILogger, JobScrapingStrategy, string, Exception?> s_logJobSearchStarting =
            LoggerMessage.Define<JobScrapingStrategy, string>(LogLevel.Information, new EventId(1, nameof(SearchJobsWithStrategyAsync)), "Executing Job Search. Strategy: {Strategy}, Query: {Query}");

        private static readonly Action<ILogger, Exception?> s_logHybridFallback =
            LoggerMessage.Define(LogLevel.Information, new EventId(2, nameof(SearchJobsWithStrategyAsync)), "Hybrid Strategy: Guest API returned no results. Falling back to Browser.");

        private static readonly Action<ILogger, int, Exception?> s_logJobSearchCompleted =
            LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(SearchJobsWithStrategyAsync)), "Job Search Completed. Found {Count} jobs.");

        private readonly Ghost.IBrowserSession _session;
        private readonly LinkedInOptions _options;
        private readonly ILogger<LinkedInJobClient> _logger;
        private readonly Internal.GuestJobSearch _guestSearch;

    public LinkedInJobClient(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInJobClient> logger, Internal.GuestJobSearch guestSearch)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInJobClient>.Instance;
        _guestSearch = guestSearch ?? throw new ArgumentNullException(nameof(guestSearch));
    }

    // Back-compat constructor used by tests and callers that don't use DI for GuestJobSearch
    public LinkedInJobClient(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInJobClient> logger)
    {
        throw new NotSupportedException("GuestJobSearch back-compat constructor removed. Provide GuestJobSearch via dependency injection.");
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

    /// <summary>
    /// Search jobs with explicit strategy override (used by API query parameter).
    /// </summary>
    public async Task<List<JobListing>> SearchJobsAsync(
        JobSearchCriteria criteria,
        JobScrapingStrategy strategyOverride,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var list = new List<JobListing>();
        await foreach (var job in SearchJobsWithStrategyAsync(criteria.Query ?? string.Empty, criteria.Location ?? "", criteria.MaxResults, strategyOverride, ct))
        {
            list.Add(job);
        }
        return list;
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
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);
        try
        {
            var url = $"{_options.BaseUrl}/jobs/view/{jobId}";
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            // Check for Easy Apply button
            bool isEasyApply = false;
            try
            {
                // Common selectors for Easy Apply
                var easyApplyBtn = await page.QuerySelectorAsync(".jobs-apply-button--top-card button, .jobs-s-apply button", ct);
                if (easyApplyBtn != null)
                {
                    var txt = await easyApplyBtn.GetTextContentAsync(ct) ?? "";
                    if (txt.Contains("Easy Apply", StringComparison.OrdinalIgnoreCase))
                    {
                        isEasyApply = true;
                    }
                }
            }
            catch { }

            // attempt to parse JSON-LD from page content
            var html = await page.GetContentAsync(ct);
            var parsed = Internal.JsonLdParser.Parse(html ?? string.Empty, jobId, url);

            // If parsed is missing or missing a title or description, attempt DOM scraping to fill missing fields.
            if (parsed == null || string.IsNullOrEmpty(parsed.Title) || string.IsNullOrEmpty(parsed.Description))
            {
                try
                {
                    // Title
                    string? title = parsed?.Title;
                    if (string.IsNullOrEmpty(title))
                    {
                        var titleEl = await page.QuerySelectorAsync(".top-card-layout__title, .job-details-jobs-unified-top-card__job-title, h1", ct);
                        if (titleEl != null)
                        {
                            var ttxt = await titleEl.GetTextContentAsync(ct);
                            title = ttxt?.Trim();
                        }
                    }

                    // Company
                    string? company = parsed?.Company;
                    if (string.IsNullOrEmpty(company))
                    {
                        var compEl = await page.QuerySelectorAsync(".top-card-layout__first-subline .topcard__org-name-link, .job-details-jobs-unified-top-card__company-name", ct);
                        if (compEl != null)
                        {
                            var ctxt = await compEl.GetTextContentAsync(ct);
                            company = ctxt?.Trim();
                        }
                    }

                    // Location
                    string? locationText = parsed?.Location;
                    if (string.IsNullOrEmpty(locationText))
                    {
                        var locEl = await page.QuerySelectorAsync(".top-card-layout__first-subline .topcard__flavor--bullet", ct);
                        if (locEl != null)
                        {
                            var ltxt = await locEl.GetTextContentAsync(ct);
                            locationText = ltxt?.Trim();
                        }
                    }

                    // Description
                    string? description = parsed?.Description;
                    if (string.IsNullOrEmpty(description))
                    {
                        var descEl = await page.QuerySelectorAsync(".show-more-less-html__markup, #job-details", ct);
                        if (descEl != null)
                        {
                            var dtxt = await descEl.GetTextContentAsync(ct);
                            description = dtxt?.Trim();
                        }
                    }

                    // PostedAt - try to find a time element with datetime attribute, otherwise fallback to UtcNow
                    DateTimeOffset postedAt = parsed?.PostedAt ?? DateTimeOffset.MinValue;
                    if (postedAt == DateTimeOffset.MinValue)
                    {
                        try
                        {
                            var timeEl = await page.QuerySelectorAsync("time[datetime]", ct);
                            string? datetimeAttr = null;
                            if (timeEl != null)
                            {
                                datetimeAttr = await timeEl.GetAttributeAsync("datetime", ct);
                            }
                            if (!string.IsNullOrEmpty(datetimeAttr) && DateTimeOffset.TryParse(datetimeAttr, out var dt))
                            {
                                postedAt = dt;
                            }
                            else
                            {
                                postedAt = DateTimeOffset.UtcNow;
                            }
                        }
                        catch
                        {
                            postedAt = DateTimeOffset.UtcNow;
                        }
                    }

                    // Merge with parsed when available - prefer parsed values per instructions
                    if (parsed != null)
                    {
                        var merged = parsed with
                        {
                            Title = string.IsNullOrEmpty(parsed.Title) ? (title ?? string.Empty) : parsed.Title,
                            Company = string.IsNullOrEmpty(parsed.Company) ? (company ?? string.Empty) : parsed.Company,
                            Location = string.IsNullOrEmpty(parsed.Location) ? (locationText ?? string.Empty) : parsed.Location,
                            Description = string.IsNullOrEmpty(parsed.Description) ? (description ?? string.Empty) : parsed.Description,
                            PostedAt = parsed.PostedAt == DateTimeOffset.MinValue ? postedAt : parsed.PostedAt,
                            Url = string.IsNullOrEmpty(parsed.Url) ? url : parsed.Url,
                            Id = string.IsNullOrEmpty(parsed.Id) ? jobId : parsed.Id,
                            IsEasyApply = isEasyApply
                        };

                        return merged;
                    }

                    // Construct new JobListing from scraped values
                    return new JobListing
                    {
                        Id = jobId,
                        Url = url,
                        Title = title ?? string.Empty,
                        Company = company ?? string.Empty,
                        Location = locationText ?? string.Empty,
                        Description = description ?? string.Empty,
                        PostedAt = postedAt == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow : postedAt,
                        IsEasyApply = isEasyApply
                    };
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                    // Fall back to parsed result if available, else minimal listing
                    if (parsed != null)
                    {
                        return parsed with { IsEasyApply = isEasyApply };
                    }
                    return new JobListing { Id = jobId, Url = url, IsEasyApply = isEasyApply };
                }
            }

            // If parsed had a title, prefer it; but ensure IsEasyApply is set
            return parsed with { IsEasyApply = isEasyApply };
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
        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);
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

            await applyBtn.HumanClickAsync(ct: ct);
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
        await foreach (var job in SearchJobsWithStrategyAsync(keywords, location, limit, _options.ScrapingStrategy, ct))
        {
            yield return job;
        }
    }

    private async IAsyncEnumerable<JobListing> SearchJobsWithStrategyAsync(string keywords, string location, int limit, JobScrapingStrategy strategy, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        s_logJobSearchStarting(_logger, strategy, keywords, null);
        // Strategy: processed according to configured scraping strategy
        if (strategy == JobScrapingStrategy.GuestApi)
        {
            // Use guest API to search then fetch details (do work outside iterator yields)
            var criteria = new Ghost.Contracts.Jobs.JobSearchCriteria { Query = keywords, Location = location, MaxResults = limit };
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

        if (strategy == JobScrapingStrategy.Hybrid)
        {
            // Try guest API first (collect results before yielding)
            var criteria = new Ghost.Contracts.Jobs.JobSearchCriteria { Query = keywords, Location = location, MaxResults = limit };
            List<string> ids = new List<string>();
            try
            {
                var raw = await _guestSearch.SearchAsync(criteria, limit, ct);
                ids = raw?.ToList() ?? new List<string>();
            }
            catch (Exception)
            {
                // Guest search failed - swallow and fallthrough to browser
                ids = new List<string>();
            }

            if (ids.Count > 0)
            {
                // guest returned results
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

                var successfulYields = 0;
                foreach (var job in results)
                {
                    yield return job;
                    successfulYields++;
                }

                    if (successfulYields > 0)
                    {
                        s_logJobSearchCompleted(_logger, successfulYields, null);
                        yield break;
                    }
            }

            // Guest API returned no results - log hybrid fallback and fallthrough to browser
            s_logHybridFallback(_logger, null);
        }

        // Add safety delay for Hybrid fallback to avoid rapid-fire detection
        if (strategy == JobScrapingStrategy.Hybrid)
        {
            await Task.Delay(2000, ct);
        }

        var pageOpts = _options.GetPageOptions();
        var page = await _session.NewPageAsync(pageOpts, ct: ct);
        try
        {
            // Clear cookies to ensure a fresh session (fixes Hybrid fallback issues)
            // Use a client-side cookie clear since the IPage abstraction doesn't expose the
            // underlying browser context in this layer.
            try
            {
                await page.EvaluateAsync<object>("() => { document.cookie.split(';').forEach(function(c) { document.cookie = c.replace(/^ +/, '').replace(/=.*/, '=;expires=' + new Date(0).toUTCString() + ';path=/'); }); }", ct: ct);
            }
            catch { }

            var q = System.Uri.EscapeDataString(keywords);
            var loc = System.Uri.EscapeDataString(location);
            var url = $"{_options.BaseUrl}/jobs/search?keywords={q}&location={loc}";
            // Starting browser navigation
            await page.NavigateAsync(url, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            // Debugging: Check title to see if we hit auth wall
            var pageTitle = await page.EvaluateAsync<string>("document.title", ct: ct);

            // Robust selector strategy: Try multiple known patterns for public/private views
            // 1. .jobs-search-results__list-item (Logged in / specific view)
            // 2. .jobs-search__results-list li (Public guest view)
            // 3. .base-card (Generic card)
            var nodes = await page.QuerySelectorAllAsync(".jobs-search-results__list-item, .jobs-search__results-list li, .base-card", ct: ct);
            // Nodes found via browser: count = nodes.Count

            // If no nodes found, continue - no debug diagnostics are emitted in production

            var count = 0;
            var list = new List<JobListing>();
            foreach (var n in nodes)
            {
                if (count++ >= limit) break;
                try
                {
                    // Selector hunting for inner details
                    
                    // ID
                    string id = Guid.NewGuid().ToString();
                    var idEl = await n.QuerySelectorAsync("[data-id], [data-entity-urn]", ct);
                    if (idEl != null)
                    {
                        var dataId = await idEl.GetAttributeAsync("data-id", ct);
                        var urn = await idEl.GetAttributeAsync("data-entity-urn", ct);
                        // Extract numeric ID from urn if possible (urn:li:jobPosting:123)
                        if (!string.IsNullOrEmpty(urn))
                        {
                            var m = System.Text.RegularExpressions.Regex.Match(urn, @"\d+");
                            if (m.Success) id = m.Value;
                        }
                        else if (!string.IsNullOrEmpty(dataId))
                        {
                            id = dataId;
                        }
                    }

                    // Title: .job-card-list__title, .base-search-card__title
                    var titleEl = await n.QuerySelectorAsync(".job-card-list__title, .base-search-card__title", ct);
                    string title = titleEl is not null ? (await titleEl.GetTextContentAsync(ct))?.Trim() ?? string.Empty : string.Empty;

                    // Company: .job-card-container__company-name, .base-search-card__subtitle
                    var companyEl = await n.QuerySelectorAsync(".job-card-container__company-name, .base-search-card__subtitle", ct);
                    string company = companyEl is not null ? (await companyEl.GetTextContentAsync(ct))?.Trim() ?? string.Empty : string.Empty;

                    // Location: .job-card-container__metadata-item, .job-search-card__location
                    var locationEl = await n.QuerySelectorAsync(".job-card-container__metadata-item, .job-search-card__location", ct);
                    string locationText = locationEl is not null ? (await locationEl.GetTextContentAsync(ct))?.Trim() ?? string.Empty : string.Empty;

                    // Link: a.base-card__full-link, .job-card-list__title
                    string? jobUrl = null;
                    var linkEl = await n.QuerySelectorAsync("a.base-card__full-link, a.job-card-list__title", ct);
                    if (linkEl != null)
                    {
                        jobUrl = await linkEl.GetAttributeAsync("href", ct);
                        
                        // If ID is still a GUID, try to extract from URL (e.g., /jobs/view/title-at-company-123456)
                        if (Guid.TryParse(id, out _) && !string.IsNullOrEmpty(jobUrl))
                        {
                            // LinkedIn job URLs end with the numeric ID before any query params
                            var urlIdMatch = System.Text.RegularExpressions.Regex.Match(jobUrl, @"-(\d{6,})(?:\?|$)");
                            if (urlIdMatch.Success && urlIdMatch.Groups[1].Success)
                            {
                                id = urlIdMatch.Groups[1].Value;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(title))
                    {
                        list.Add(new JobListing 
                        { 
                            Id = id, 
                            Title = title, 
                            Company = company, 
                            Location = locationText,
                            Url = jobUrl 
                        });
                    }
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                }
            }
            
            // Deduplicate by job ID (same card can match multiple selectors)
            var uniqueJobs = list.GroupBy(j => j.Id).Select(g => g.First()).ToList();

            foreach (var job in uniqueJobs)
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
