using Ghost.Contracts.Jobs;
using Ghost.Extensions;
using Ghost.Platform.LinkedIn.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Ghost.Platform.LinkedIn;

/// <summary>
/// Job search client for LinkedIn.
/// </summary>
    public sealed class LinkedInJobClient : Ghost.Abstractions.IJobScraper
    {
        private static readonly Action<ILogger, JobScrapingStrategy, string, Exception?> s_logJobSearchStarting =
            LoggerMessage.Define<JobScrapingStrategy, string>(LogLevel.Information, new EventId(1, nameof(SearchJobsWithStrategyAsync)), "Executing Job Search. Strategy: {Strategy}, Query: {Query}");

        private static readonly Action<ILogger, Exception?> s_logHybridFallback =
            LoggerMessage.Define(LogLevel.Information, new EventId(2, nameof(SearchJobsWithStrategyAsync)), "Hybrid Strategy: Guest API returned no results. Falling back to Browser.");

        private static readonly Action<ILogger, int, Exception?> s_logJobSearchCompleted =
            LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(SearchJobsWithStrategyAsync)), "Job Search Completed. Found {Count} jobs.");

        private static readonly Action<ILogger, string, Exception?> s_logDeepFetchFailed =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, nameof(SearchJobsWithStrategyAsync)), "Failed to deep fetch details for job {Id}. Returning shallow.");

        private static readonly Action<ILogger, string, Exception?> s_logZeroJobsFound =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, nameof(SearchJobsWithStrategyAsync)), "Browser search found 0 jobs. Page Title: {Title}");

        // Common selector sets used by DOM scraping - defined as static readonly to satisfy CA1861
        private static readonly string[] s_titleSelectors = new[] { ".top-card-layout__title", ".job-details-jobs-unified-top-card__job-title", "h1", ".job-card-list__title" };
        private static readonly string[] s_companySelectors = new[] { ".top-card-layout__first-subline .topcard__org-name-link", ".job-details-jobs-unified-top-card__company-name", ".topcard__org-name-link", ".job-card-container__company-name", ".top-card-layout__company-url", "a[data-tracking-control-name='public_jobs_topcard-org-name']" };
        private static readonly string[] s_locationSelectors = new[] { ".top-card-layout__first-subline .topcard__flavor--bullet", ".job-details-jobs-unified-top-card__bullet", ".topcard__flavor--bullet", ".job-search-card__location", ".job-card-container__metadata-item" };
        private static readonly string[] s_descriptionSelectors = new[] { ".show-more-less-html__markup", "#job-details", ".description__text", ".job-description", ".core-section-container__content" };
        private static readonly char[] s_labelValueSplit = new[] { '\n', ':' };
        private static readonly char[] s_newlines = new[] { '\n', '\r' };

    private static JobType ParseJobType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return JobType.Unknown;
        var normalized = type.ToUpperInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
        // Simple containment checks often work better for messy scraped text
        if (normalized.Contains("FULLTIME")) return JobType.FullTime;
        if (normalized.Contains("PARTTIME")) return JobType.PartTime;
        if (normalized.Contains("CONTRACT")) return JobType.Contract;
        if (normalized.Contains("TEMPORARY")) return JobType.Contract;
        if (normalized.Contains("INTERN")) return JobType.Internship;

        return normalized switch
        {
            "FULLTIME" => JobType.FullTime,
            "PARTTIME" => JobType.PartTime,
            "CONTRACT" => JobType.Contract,
            "TEMPORARY" => JobType.Contract,
            "INTERN" => JobType.Internship,
            "INTERNSHIP" => JobType.Internship,
            _ => JobType.Unknown
        };
    }

    private static ExperienceLevel ParseExperienceLevel(string? level)
    {
        if (string.IsNullOrEmpty(level)) return ExperienceLevel.Unknown;
        var n = level.Trim().ToLowerInvariant();
        if (n.Contains("intern")) return ExperienceLevel.EntryLevel; // map internship to entry level
        if (n.Contains("entry")) return ExperienceLevel.EntryLevel;
        if (n.Contains("associate")) return ExperienceLevel.MidLevel;
        if (n.Contains("mid")) return ExperienceLevel.MidLevel;
        if (n.Contains("senior")) return ExperienceLevel.Senior;
        if (n.Contains("director") || n.Contains("manager") || n.Contains("executive")) return ExperienceLevel.Manager;
        return ExperienceLevel.Unknown;
    }

        private readonly Ghost.IBrowserSession _session;
        private readonly LinkedInOptions _options;
        private readonly ILogger<LinkedInJobClient> _logger;
        private readonly Internal.IGuestJobSearch _guestSearch;

    public LinkedInJobClient(Ghost.IBrowserSession session, IOptions<LinkedInOptions> options, ILogger<LinkedInJobClient> logger, Internal.IGuestJobSearch guestSearch)
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
        // Resolve strategy override from criteria if present; fall back to configured option
        var strategy = _options.ScrapingStrategy;
        if (!string.IsNullOrEmpty(criteria.Strategy))
        {
            if (Enum.TryParse<JobScrapingStrategy>(criteria.Strategy, ignoreCase: true, out var parsed))
            {
                strategy = parsed;
            }
        }

        var e = SearchJobsWithStrategyAsync(criteria.Query ?? string.Empty, criteria.Location ?? string.Empty, criteria.MaxResults, strategy, ct);
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
        var list = new List<JobListing>();
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

            // NOTE: debug artifacts removed - do not write files during parsing

            var extractor = new Ghost.Utilities.JsonLdExtractor();
            var parser = new Internal.JsonLdParser(extractor);
            var parsed = parser.Parse(html ?? string.Empty, jobId, url);

            // If parsed is missing or missing a title or description (or company), attempt DOM scraping to fill missing fields.
            if (parsed == null || string.IsNullOrEmpty(parsed.Title) || string.IsNullOrEmpty(parsed.Description) || string.IsNullOrEmpty(parsed.Company))
            {
                try
                {
                // Local helper for robust scraping
                async Task<string?> ScrapeFirstAsync(string[] selectors)
                {
                    foreach (var sel in selectors)
                    {
                        try
                        {
                            var el = await page.QuerySelectorAsync(sel, ct);
                            if (el != null)
                            {
                                var txt = await el.GetTextContentAsync(ct);
                                if (!string.IsNullOrWhiteSpace(txt)) return txt.Trim();
                            }
                        }
                        catch { }
                    }
                    return null;
                }

                // Title
                string? title = parsed?.Title;
                if (string.IsNullOrEmpty(title))
                {
                    title = await ScrapeFirstAsync(s_titleSelectors);
                }

                // Company
                string? company = parsed?.Company;
                if (string.IsNullOrEmpty(company))
                {
                    company = await ScrapeFirstAsync(s_companySelectors);
                    
                    // Regex fallback for Company
                    if (string.IsNullOrEmpty(company) && html != null)
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(html, "class=\"[^\"]*topcard__org-name-link[^\"]*\">\\s*([^<]+)\\s*<", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (m.Success) company = m.Groups[1].Value.Trim();
                    }
                }

                // Location
                string? locationText = parsed?.Location;
                if (string.IsNullOrEmpty(locationText))
                {
                    locationText = await ScrapeFirstAsync(s_locationSelectors);

                    // Regex fallback for Location
                    if (string.IsNullOrEmpty(locationText) && html != null)
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(html, "class=\"[^\"]*topcard__flavor--bullet[^\"]*\">\\s*([^<]+)\\s*<", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                        if (m.Success) locationText = m.Groups[1].Value.Trim();
                    }
                }

                // Description
                string? description = parsed?.Description;
                if (string.IsNullOrEmpty(description))
                {
                    description = await ScrapeFirstAsync(s_descriptionSelectors);
                }

                    // Scrape criteria list for Employment type and Seniority level
                    JobType scrapedJobType = JobType.Unknown;
                    ExperienceLevel scrapedExperienceLevel = ExperienceLevel.Unknown;
                    string? scrapedSalary = null;
                    try
                    {
                        var critNodes = await page.QuerySelectorAllAsync(".description__job-criteria-list .description__job-criteria-item, .description__job-criteria-list li, .job-details-jobs-unified-top-card__job-insight", ct);
                        foreach (var c in critNodes)
                        {
                            try
                            {
                                var txt = (await c.GetTextContentAsync(ct))?.Trim() ?? string.Empty;
                                if (string.IsNullOrEmpty(txt)) continue;

                                // Normalize and split by newline-first (handles layouts that use lines instead of colons)
                                var lines = txt.Split(s_newlines, StringSplitOptions.RemoveEmptyEntries).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();
                                string label = string.Empty;
                                string value = string.Empty;
                                if (lines.Length >= 2)
                                {
                                    label = lines[0];
                                    value = lines[1];
                                }
                                else
                                {
                                    // Fallback to split on first ':' as legacy behavior
                                    var parts = txt.Split(s_newlines, 2);
                                    label = parts.Length > 0 ? parts[0].Trim() : string.Empty;
                                    value = parts.Length > 1 ? parts[1].Trim() : string.Empty;
                                }

                                if (label.Contains("Employment", StringComparison.OrdinalIgnoreCase) || txt.Contains("Employment type", StringComparison.OrdinalIgnoreCase))
                                {
                                    var jt = ParseJobType(value);
                                    if (jt != JobType.Unknown) scrapedJobType = jt;
                                }

                                if (label.Contains("Seniority", StringComparison.OrdinalIgnoreCase) || txt.Contains("Seniority level", StringComparison.OrdinalIgnoreCase))
                                {
                                    // Handle Not Applicable explicitly
                                    if (value?.Trim().Equals("Not Applicable", StringComparison.OrdinalIgnoreCase) == true)
                                    {
                                        scrapedExperienceLevel = ExperienceLevel.Unknown;
                                    }
                                    else
                                    {
                                        var el = ParseExperienceLevel(value);
                                        if (el != ExperienceLevel.Unknown) scrapedExperienceLevel = el;
                                    }
                                }
                            }
                            catch { }
                        }

                        // Salary: attempt to find salary block in page
                        try
                        {
                            var salEl = await page.QuerySelectorAsync(".main-job-card__salary-info, .main-job-card__salary-info", ct);
                            if (salEl != null)
                            {
                                var raw = await salEl.GetTextContentAsync(ct);
                                if (!string.IsNullOrWhiteSpace(raw))
                                {
                                    // collapse whitespace and newlines into single spaces
                                    var cleaned = System.Text.RegularExpressions.Regex.Replace(raw, "\\s+", " ").Trim();
                                    // normalize hyphen spacing
                                    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "\\s*-\\s*", " - ");
                                    scrapedSalary = cleaned;
                                }
                            }
                        }
                        catch { }
                    }
                    catch { }

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
                            IsEasyApply = isEasyApply,
                            JobType = parsed.JobType == JobType.Unknown ? scrapedJobType : parsed.JobType,
                            ExperienceLevel = parsed.ExperienceLevel == ExperienceLevel.Unknown ? scrapedExperienceLevel : parsed.ExperienceLevel,
                            Salary = string.IsNullOrWhiteSpace(parsed.Salary) ? scrapedSalary : parsed.Salary,
                            Source = "LinkedIn"
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
                        IsEasyApply = isEasyApply,
                        JobType = scrapedJobType,
                        ExperienceLevel = scrapedExperienceLevel,
                        Salary = scrapedSalary,
                        Source = "LinkedIn"
                    };
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                    // Fall back to parsed result if available, else minimal listing
                    if (parsed != null)
                    {
                        return parsed with { IsEasyApply = isEasyApply, Source = "LinkedIn" };
                    }
                    return new JobListing { Id = jobId, Url = url, IsEasyApply = isEasyApply, Source = "LinkedIn" };
                }
            }

            // If parsed had a title, prefer it; but ensure IsEasyApply is set
            return parsed with { IsEasyApply = isEasyApply, Source = "LinkedIn" };
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
        var list = new List<JobListing>();
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

    /// <summary>
    /// Searches jobs in parallel with bounded concurrency (max 3 requests).
    /// </summary>
    public async IAsyncEnumerable<JobListing> SearchJobsParallelAsync(
        JobSearchCriteria criteria,
        [EnumeratorCancellation] CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        var limit = criteria.MaxResults > 0 ? criteria.MaxResults : 25;
        var keywords = criteria.Query ?? string.Empty;
        var location = criteria.Location ?? string.Empty;

        var firstPage = await FetchGuestPageAsync(criteria, 0, ct).ConfigureAwait(false);
        foreach (var job in firstPage.Jobs)
        {
            yield return job;
        }

        if (firstPage.TotalAvailable <= firstPage.PageSize || limit <= firstPage.PageSize)
        {
            yield break;
        }

        var total = Math.Min(firstPage.TotalAvailable, limit);
        var totalPages = (int)Math.Ceiling(total / (double)firstPage.PageSize);
        if (totalPages <= 1)
        {
            yield break;
        }

        var semaphore = new SemaphoreSlim(3, 3);
        var tasks = new List<Task<GuestPageResult>>();
        for (var pageIndex = 1; pageIndex < totalPages; pageIndex++)
        {
            var offset = pageIndex * firstPage.PageSize;
            tasks.Add(FetchGuestPageAsync(criteria, offset, ct, semaphore));
        }

        var remaining = new HashSet<Task<GuestPageResult>>(tasks);
        while (remaining.Count > 0)
        {
            var completed = await Task.WhenAny(remaining).ConfigureAwait(false);
            remaining.Remove(completed);
            var page = await completed.ConfigureAwait(false);
            foreach (var job in page.Jobs)
            {
                yield return job;
            }
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
        var list = new List<JobListing>();
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

            // If no nodes found, emit a warning with the page title to help diagnose auth walls
            if (nodes.Count == 0)
            {
                s_logZeroJobsFound(_logger, pageTitle ?? "Unknown", null);
            }

            var count = 0;
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
                                    Url = jobUrl,
                                    Source = "LinkedIn"
                                });
                            }
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                }
            }
            
            // Note: do not yield shallow results here. We close the page and then
            // deep-fetch details for each job to produce parity with GuestApi.
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }

        // After page is disposed, attempt deep-fetching details for each unique job.
        var uniqueJobs = list.GroupBy(j => j.Id).Select(g => g.First()).ToList();
        foreach (var shallow in uniqueJobs)
        {
            ct.ThrowIfCancellationRequested();
            JobListing? deepJob = null;
            try
            {
                deepJob = await GetJobDetailsBrowserAsync(shallow.Id, ct);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                s_logDeepFetchFailed(_logger, shallow.Id, ex);
            }

            yield return deepJob ?? shallow;
        }
    }

    private async Task<GuestPageResult> FetchGuestPageAsync(JobSearchCriteria criteria, int offset, CancellationToken ct, SemaphoreSlim? semaphore = null)
    {
        if (semaphore is not null)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
        }

        try
        {
            var pageSize = 25;
            var baseUrl = _options.BaseUrl;
            var query = criteria.Query ?? string.Empty;
            var location = criteria.Location ?? string.Empty;
            var postedWithin = criteria.PostedDate switch
            {
                TimePosted.Past24Hours => TimeSpan.FromDays(1),
                TimePosted.PastWeek => TimeSpan.FromDays(7),
                TimePosted.PastMonth => TimeSpan.FromDays(30),
                _ => (TimeSpan?)null
            };

            var pageOpts = _options.GetPageOptions();
            var page = await _session.NewPageAsync(pageOpts, ct: ct).ConfigureAwait(false);
            try
            {
                var searchUrl = LinkedInQueryBuilder.BuildSearchUrl(query, location, offset, postedWithin);
                if (!string.IsNullOrWhiteSpace(baseUrl) && !baseUrl.Equals("https://www.linkedin.com", StringComparison.OrdinalIgnoreCase))
                {
                    var relative = searchUrl.Replace("https://www.linkedin.com", string.Empty, StringComparison.OrdinalIgnoreCase);
                    searchUrl = baseUrl.TrimEnd('/') + relative;
                }

                await page.NavigateAsync(searchUrl, ct: ct).ConfigureAwait(false);
                await page.WaitForLoadStateAsync(ct: ct).ConfigureAwait(false);
                var html = await page.GetContentAsync(ct).ConfigureAwait(false) ?? string.Empty;
                var ids = ParseGuestSearchIds(html);
                var total = ParseGuestTotalCount(html, ids.Count);

                var jobs = new ConcurrentBag<JobListing>();
                foreach (var id in ids)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var job = await _guestSearch.FetchJobDetailsAsync(id, ct).ConfigureAwait(false);
                        if (job != null)
                        {
                            jobs.Add(job);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                    }
                }

                return new GuestPageResult(jobs.ToList(), total, pageSize);
            }
            finally
            {
                try { await page.DisposeAsync(); } catch { }
            }
        }
        finally
        {
            semaphore?.Release();
        }
    }

    private static List<string> ParseGuestSearchIds(string html)
    {
        var ids = new List<string>();

        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(html, "data-entity-urn=\"urn:li:jobPosting:(?<id>[0-9]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }

        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(html, "/jobs/(?:view|r)/(?<id>[0-9]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
        }

        return ids;
    }

    private static int ParseGuestTotalCount(string html, int fallback)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return fallback;
        }

        var match = System.Text.RegularExpressions.Regex.Match(html, "results-context-header__job-count\"[^>]*>(?<count>[0-9,]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups["count"].Value.Replace(",", string.Empty, StringComparison.Ordinal), out var total))
        {
            return total;
        }

        return fallback;
    }

    private sealed record GuestPageResult(IReadOnlyList<JobListing> Jobs, int TotalAvailable, int PageSize);
}
