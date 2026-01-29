using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Ghost.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;
using System.Linq;

namespace Ghost.Platform.LinkedIn.Internal;

public sealed class GuestJobSearch : IGuestJobSearch
{
    private readonly GhostKernel _kernel;
    private readonly IProxyProvider _proxyProvider;
    private readonly ILogger<GuestJobSearch> _logger;
    private readonly IOptions<LinkedInOptions> _options;
    private readonly LinkedInAuthenticator _authenticator;
    private readonly ICountryDomainProvider _countryProvider;

    private static readonly Action<ILogger, string, Exception?> s_logUsingProxy =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(GuestJobSearch)), "Using proxy: {Proxy}");

    private static readonly Action<ILogger, string, Exception?> s_logNavigating =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, nameof(GuestJobSearch)), "Navigating to: {Url}");

    private static readonly Action<ILogger, string, bool, Exception?> s_logSessionCreating =
        LoggerMessage.Define<string, bool>(LogLevel.Information, new EventId(5, nameof(SearchAsync)), "Creating isolated session. Proxy: {Proxy}, Warm-up: {WarmUp}");

    private static readonly Action<ILogger, string, Exception?> s_logRateLimitPassed =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, nameof(SearchAsync)), "Rate limit check passed for {Url}");

    private static readonly Action<ILogger, string, Exception?> s_logSavingSession =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(7, nameof(SearchAsync)), "Saving session state to {Path}");

    private static readonly Action<ILogger, Exception?> s_logGuestSearchFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(GuestJobSearch)), "Guest search navigation/parsing failed");

    private static readonly Action<ILogger, Exception?> s_logProxyDisabled =
        LoggerMessage.Define(LogLevel.Information, new EventId(4, nameof(GuestJobSearch)), "Proxy disabled by configuration. Using direct connection.");

    private static readonly Action<ILogger, string, int, string, Exception?> s_logProxyFailed =
        LoggerMessage.Define<string, int, string>(LogLevel.Warning, new EventId(8, nameof(GuestJobSearch)), "Proxy {Proxy} failed (Attempt {Attempt}/3). Error: {Message}");

    private static readonly Action<ILogger, string, Exception?> s_logAllProxyFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(9, nameof(GuestJobSearch)), "All proxy attempts failed for {Url}");

    private static readonly char[] s_newlines = { '\n', '\r' };

    public GuestJobSearch(
        GhostKernel kernel,
        IProxyProvider proxyProvider,
        IOptions<LinkedInOptions> options,
        LinkedInAuthenticator authenticator,
        ILogger<GuestJobSearch> logger,
        ICountryDomainProvider countryProvider)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(proxyProvider);
        _kernel = kernel;
        _proxyProvider = proxyProvider;
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GuestJobSearch>.Instance;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _countryProvider = countryProvider ?? throw new ArgumentNullException(nameof(countryProvider));
    }

    public async Task<IReadOnlyList<string>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var ids = new List<string>();
        // Create a fresh session for the search to isolate from other work
        SessionOptions options;
        string proxyUsed = "None";
        // Preserve storage state path from options so sessions can reuse cookies if configured
        if (!_options.Value.ProxyEnabled)
        {
            // When proxy usage is disabled by configuration, do not fetch a proxy
            // and create session options without proxy settings.
            s_logProxyDisabled(_logger, null);
            options = new SessionOptions { Proxy = null, StorageStatePath = _options.Value.StorageStatePath };
            proxyUsed = "Disabled";
        }
        else
        {
            // Keep existing behavior: fetch a proxy and apply settings to the session
            var proxy = _proxyProvider is not null ? await _proxyProvider.GetProxyAsync("US", ct) : null;
            // log the proxy being used for this search
            s_logUsingProxy(_logger, proxy?.Server ?? "None", null);
            options = new SessionOptions { StorageStatePath = _options.Value.StorageStatePath };
            if (proxy is not null)
            {
                options.Proxy = new SessionOptions.ProxySettings(proxy.Server, proxy.Username, proxy.Password);
                proxyUsed = proxy.Server ?? "None";
            }
            else
            {
                proxyUsed = "None";
            }
        }

        var q = Uri.EscapeDataString(criteria.Query ?? string.Empty);
        var loc = Uri.EscapeDataString(criteria.Location ?? string.Empty);

        for (var offset = 0; ids.Count < limit; offset += 25)
        {
            ct.ThrowIfCancellationRequested();

            // Build base URL and append time filter if present
            var baseUrlDomain = _countryProvider.GetDomain(_options.Value.Country);
            var baseUrl = $"{baseUrlDomain}/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords={q}&location={loc}&start={offset}";
            string? tpr = criteria.PostedDate switch
            {
                TimePosted.Past24Hours => "r86400",
                TimePosted.PastWeek => "r604800",
                TimePosted.PastMonth => "r2592000",
                _ => null
            };
            var url = tpr is not null ? baseUrl + $"&f_TPR={tpr}" : baseUrl;

            List<string>? found = null;
            var success = false;

            // Try up to 3 attempts, fetching a fresh proxy/session each time
            for (var attempt = 1; attempt <= 3 && !success; attempt++)
            {
                SessionOptions attemptOptions;
                string attemptProxy = "None";

                if (!_options.Value.ProxyEnabled)
                {
                    s_logProxyDisabled(_logger, null);
                    attemptOptions = new SessionOptions { Proxy = null, StorageStatePath = _options.Value.StorageStatePath };
                    attemptProxy = "Disabled";
                }
                else
                {
                    var proxy = _proxyProvider is not null ? await _proxyProvider.GetProxyAsync("US", ct) : null;
                    s_logUsingProxy(_logger, proxy?.Server ?? "None", null);
                    attemptOptions = new SessionOptions { StorageStatePath = _options.Value.StorageStatePath };
                    if (proxy is not null)
                    {
                        attemptOptions.Proxy = new SessionOptions.ProxySettings(proxy.Server, proxy.Username, proxy.Password);
                        attemptProxy = proxy.Server ?? "None";
                    }
                    else
                    {
                        attemptProxy = "None";
                    }
                }

                s_logSessionCreating(_logger, attemptProxy, _options.Value.WarmUpEnabled, null);
                var session = await _kernel.NewSessionAsync(attemptOptions, ct);
                var page = await session.NewPageAsync(ct: ct);
                try
                {
                    s_logNavigating(_logger, url, null);
                    if (_options.Value.WarmUpEnabled)
                    {
                        try { await _authenticator.WarmUpAsync(page, ct); } catch { }
                    }

                    await page.NavigateAsync(url, ct: ct);

                    try
                    {
                        await LinkedInRateLimitDetector.CheckAsync(page);
                        s_logRateLimitPassed(_logger, url, null);
                    }
                    catch { }

                        var html = await page.GetContentAsync(ct);
                    if (string.IsNullOrEmpty(html))
                    {
                        success = true;
                        break;
                    }

                    if (html.Contains("429 Too Many Requests", StringComparison.OrdinalIgnoreCase) || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                    {
                        LinkedInLogGuest.LogGuestApiThrottled(_logger);
                        success = true;
                        break;
                    }

                    found = ExtractIdsFromSearchHtml(html);
                    if (found.Count == 0)
                    {
                        success = true;
                        break;
                    }

                    if (found.Count > 0 && !string.IsNullOrEmpty(_options.Value.StorageStatePath))
                    {
                        try { s_logSavingSession(_logger, _options.Value.StorageStatePath, null); await session.SaveStorageStateAsync(_options.Value.StorageStatePath); } catch { }
                    }

                    foreach (var id in found)
                    {
                        if (ids.Count >= limit) break;
                        if (!ids.Contains(id)) ids.Add(id);
                    }

                    success = true;
                }
                catch (OperationCanceledException) { throw; }
                catch (PlaywrightException pex)
                {
                    // Any Playwright error during navigation/setup should trigger a proxy retry.
                    s_logProxyFailed(_logger, attemptProxy, attempt, pex.Message, null);
                    try { await page.DisposeAsync(); } catch { }
                    try { await session.DisposeAsync(); } catch { }
                    // continue to next attempt which will fetch a new proxy
                    continue;
                }
                catch (Exception ex)
                {
                    s_logGuestSearchFailed(_logger, ex);
                    LinkedInLog.LogFailedToParseSearchNode(_logger, ex);
                    // do not retry other exceptions
                    success = true;
                    break;
                }
                finally
                {
                    try { await page.DisposeAsync(); } catch { }
                    if (session is not null)
                    {
                        try { await session.DisposeAsync(); } catch { }
                    }
                }
            }

            if (!success)
            {
                s_logAllProxyFailed(_logger, url, null);
                return ids;
            }

            if (found is null || found.Count == 0) break;

            if (found.Count < 25) break;
        }

        return ids;
    }

    public async Task<JobListing?> FetchJobDetailsAsync(string jobId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jobId);
        // Try up to 3 attempts, recreating session on Playwright failures (proxy tunnel issues etc.)
        var domain = _countryProvider.GetDomain(_options.Value.Country);
        var url = $"{domain}/jobs-guest/jobs/api/jobPosting/{jobId}";
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            SessionOptions attemptOptions;
            string attemptProxy = "None";

            if (!_options.Value.ProxyEnabled)
            {
                s_logProxyDisabled(_logger, null);
                attemptOptions = new SessionOptions { Proxy = null, StorageStatePath = _options.Value.StorageStatePath };
                attemptProxy = "Disabled";
            }
            else
            {
                var proxy = _proxyProvider is not null ? await _proxyProvider.GetProxyAsync("US", ct) : null;
                s_logUsingProxy(_logger, proxy?.Server ?? "None", null);
                attemptOptions = new SessionOptions { StorageStatePath = _options.Value.StorageStatePath };
                if (proxy is not null)
                {
                    attemptOptions.Proxy = new SessionOptions.ProxySettings(proxy.Server, proxy.Username, proxy.Password);
                    attemptProxy = proxy.Server ?? "None";
                }
                else
                {
                    attemptProxy = "None";
                }
            }

            s_logSessionCreating(_logger, attemptProxy, _options.Value.WarmUpEnabled, null);
            var session = await _kernel.NewSessionAsync(attemptOptions, ct);
            var page = await session.NewPageAsync(ct: ct);
            try
            {
                try
                {
                    s_logNavigating(_logger, url, null);
                    if (_options.Value.WarmUpEnabled)
                    {
                        try { await _authenticator.WarmUpAsync(page, ct); } catch { }
                    }

                    await page.NavigateAsync(url, ct: ct);
                    try { await LinkedInRateLimitDetector.CheckAsync(page); } catch { }
                    Console.WriteLine($"[DEBUG] Fetching content for {jobId}...");
                    var html = await page.GetContentAsync(ct);
                    
                    // NOTE: debug artifacts removed - production code should not write files during parsing
                    if (string.IsNullOrEmpty(html)) return null;

                    if (html.Contains("429", StringComparison.OrdinalIgnoreCase) || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                    {
                        LinkedInLogGuest.LogGuestJobEndpointThrottled(_logger, jobId);
                        return null;
                    }

                    // Use the JsonLdExtractor implementation from Ghost.Utilities via DI/Activator
                    var extractor = (Ghost.Abstractions.IJsonLdExtractor?)Activator.CreateInstance(Type.GetType("Ghost.Utilities.JsonLdExtractor, Ghost.Core" ) ?? typeof(Ghost.Utilities.JsonLdExtractor));
                    var parser = new JsonLdParser(extractor!);
                    var parsed = parser.Parse(html, jobId, url);

                    // If JSON-LD parsing failed to extract critical fields, fall back to DOM scraping
                    // We check Description, Company, or Location as primary signals of a good parse
                    if (parsed is null || 
                        string.IsNullOrEmpty(parsed.Description) || 
                        string.IsNullOrEmpty(parsed.Company) ||
                        string.IsNullOrEmpty(parsed.Location))
                    {
                        try { Console.WriteLine($"[DEBUG] Entered fallback for {jobId}"); } catch { }
                        // Helper to scrape first non-empty selector text
                        static async Task<string?> ScrapeFirstAsync(IPage p, string[] selectors, CancellationToken ct)
                        {
                            foreach (var sel in selectors)
                            {
                                ct.ThrowIfCancellationRequested();
                                try
                                {
                                    var handle = await p.QuerySelectorAsync(sel, ct);
                                    if (handle is null) continue;
                                    var txt = await handle.GetTextContentAsync(ct);
                                    if (!string.IsNullOrWhiteSpace(txt)) return txt?.Trim();
                                }
                                catch { }
                            }
                            return null;
                        }

                        // Robust selectors for guest view (updated 2026)
                        var descSelectors = new[] { 
                            ".show-more-less-html__markup", 
                            ".description__text", 
                            "#job-details",
                            ".job-description",
                            ".core-section-container__content"
                        };
                        
                        var titleSelectors = new[] { 
                            ".top-card-layout__title", 
                            ".top-card-layout__entity-info h1",
                            "h1" 
                        };
                        
                        var companySelectors = new[] { 
                            ".top-card-layout__first-subline .topcard__org-name-link",
                            ".top-card-layout__company-url",
                            "a[data-tracking-control-name='public_jobs_topcard-org-name']",
                            ".job-details-jobs-unified-top-card__company-name",
                            ".topcard__org-name-link"
                        };
                        
                        var locationSelectors = new[] { 
                            ".top-card-layout__first-subline .topcard__flavor:not(.topcard__org-name-link)",
                            ".top-card-layout__first-subline .topcard__flavor--bullet",
                            ".job-details-jobs-unified-top-card__bullet",
                            ".job-search-card__location",
                            ".topcard__flavor--bullet"
                        };

                        var scrapedDescription = await ScrapeFirstAsync(page, descSelectors, ct);
                        var scrapedTitle = await ScrapeFirstAsync(page, titleSelectors, ct);
                        var scrapedCompany = await ScrapeFirstAsync(page, companySelectors, ct);
                        var scrapedLocation = await ScrapeFirstAsync(page, locationSelectors, ct);

                        // Try to scrape criteria for JobType/Experience
                        string? scrapedJobType = null;
                        string? scrapedExperience = null;
                        
                        // Prefer the newer criteria item structure
                        var criteriaList = await page.QuerySelectorAllAsync(".description__job-criteria-list .description__job-criteria-item, .description__job-criteria-list li, .job-details-jobs-unified-top-card__job-insight", ct);
                        foreach (var item in criteriaList)
                        {
                            try {
                                var text = await item.GetTextContentAsync(ct);
                                if (!string.IsNullOrEmpty(text))
                                {
                                    // Normalize and split lines - header on first line, value on second
                                    var parts = text.Split(s_newlines, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                                    if (parts.Length >= 2)
                                    {
                                        var header = parts[0];
                                        var value = parts[1];
                                        if (header.Contains("Employment", StringComparison.OrdinalIgnoreCase) || header.Contains("Employment type", StringComparison.OrdinalIgnoreCase)) scrapedJobType = value;
                                        else if (header.Contains("Seniority", StringComparison.OrdinalIgnoreCase) || header.Contains("Seniority level", StringComparison.OrdinalIgnoreCase)) scrapedExperience = value;
                                    }
                                }
                            } catch {}
                        }

                        // Salary: attempt to find salary block in guest view using multiple selectors
                        string? scrapedSalary = null;
                        try
                        {
                            var salarySelectors = new[] {
                                ".main-job-card__salary-info",
                                ".job-details-jobs-unified-top-card__salary",
                                ".job-details-jobs-unified-top-card__salary-info",
                                ".description__job-criteria-item--salary",
                                ".description__job-criteria-item:has(span:contains('Salary'))",
                                ".salary-range",
                                ".salary",
                                ".job-criteria__item--salary"
                            };

                            // Try each selector until we find a non-empty text
                            foreach (var sel in salarySelectors)
                            {
                                try
                                {
                                    var el = await page.QuerySelectorAsync(sel, ct);
                                    if (el is null) continue;
                                    var raw = await el.GetTextContentAsync(ct);
                                    if (string.IsNullOrWhiteSpace(raw)) continue;
                                    var cleaned = System.Text.RegularExpressions.Regex.Replace(raw, "\\s+", " ").Trim();
                                    cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, "\\s*-\\s*", " - ");
                                    scrapedSalary = cleaned;
                                    break;
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // PostedAt: try to parse an explicit datetime or relative 'X days ago' text
                        DateTimeOffset? scrapedPostedAt = null;
                        try
                        {
                            var postedSelectors = new[] {
                                "time[datetime]",
                                "time",
                                ".posted-time-ago__text",
                                ".topcard__flavor--metadata time",
                                ".job-details-jobs-unified-top-card__posted-date",
                                "span.posted-time-ago__text"
                            };

                            foreach (var sel in postedSelectors)
                            {
                                try
                                {
                                    var el = await page.QuerySelectorAsync(sel, ct);
                                    if (el is null) continue;
                                    var dtAttr = await el.GetAttributeAsync("datetime", ct);
                                    if (!string.IsNullOrWhiteSpace(dtAttr) && DateTimeOffset.TryParse(dtAttr, out var dto))
                                    {
                                        scrapedPostedAt = dto;
                                        break;
                                    }

                                    var txt = await el.GetTextContentAsync(ct);
                                    if (string.IsNullOrWhiteSpace(txt)) continue;

                                    // Try absolute parse first
                                    if (DateTimeOffset.TryParse(txt, out var dtParsed))
                                    {
                                        scrapedPostedAt = dtParsed;
                                        break;
                                    }

                                    // Try relative times like '3 days ago' or 'Posted 4 hours ago'
                                    var m = Regex.Match(txt, "(?<n>\\d+)\\s*(minute|minutes|hour|hours|day|days|week|weeks|month|months|year|years)\\s*ago", RegexOptions.IgnoreCase);
                                    if (m.Success && int.TryParse(m.Groups["n"].Value, out var n))
                                    {
                                        var unit = m.Groups[2].Value.ToLowerInvariant();
                                        TimeSpan delta = unit.StartsWith("minute", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromMinutes(n)
                                            : unit.StartsWith("hour", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromHours(n)
                                            : unit.StartsWith("day", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromDays(n)
                                            : unit.StartsWith("week", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromDays(7 * n)
                                            : unit.StartsWith("month", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromDays(30 * n)
                                            : TimeSpan.FromDays(365 * n);
                                        scrapedPostedAt = DateTimeOffset.UtcNow - delta;
                                        break;
                                    }
                                }
                                catch { }
                            }
                        }
                        catch { }

                        // Regex fallback if selectors failed
                        if (string.IsNullOrWhiteSpace(scrapedCompany))
                        {
                            var m = Regex.Match(html, "class=\"[^\"]*topcard__org-name-link[^\"]*\">\\s*([^<]+)\\s*<", RegexOptions.IgnoreCase);
                            if (m.Success) scrapedCompany = m.Groups[1].Value.Trim();
                        }
                        if (string.IsNullOrWhiteSpace(scrapedLocation))
                        {
                            var m = Regex.Match(html, "class=\"[^\"]*topcard__flavor--bullet[^\"]*\">\\s*([^<]+)\\s*<", RegexOptions.IgnoreCase);
                            if (m.Success) scrapedLocation = m.Groups[1].Value.Trim();
                        }

                        if (parsed is null)
                        {
                            parsed = new JobListing
                            {
                                Id = jobId,
                                Description = scrapedDescription,
                                Title = scrapedTitle ?? string.Empty,
                                Company = scrapedCompany ?? string.Empty,
                                Location = scrapedLocation,
                                Url = url,
                                JobType = ParseJobType(scrapedJobType),
                                ExperienceLevel = ParseExperienceLevel(scrapedExperience),
                                PostedAt = scrapedPostedAt ?? DateTimeOffset.UtcNow,
                                Salary = scrapedSalary,
                                Source = "LinkedIn"
                            };
                        }
                        else
                        {
                            var desc = string.IsNullOrWhiteSpace(parsed.Description) ? scrapedDescription : parsed.Description;
                            var title = string.IsNullOrWhiteSpace(parsed.Title) ? (scrapedTitle ?? parsed.Title) : parsed.Title;
                            var company = string.IsNullOrWhiteSpace(parsed.Company) ? (scrapedCompany ?? parsed.Company) : parsed.Company;
                            var location = string.IsNullOrWhiteSpace(parsed.Location) ? scrapedLocation : parsed.Location;
                            var jType = parsed.JobType == JobType.Unknown ? ParseJobType(scrapedJobType) : parsed.JobType;
                            var exp = parsed.ExperienceLevel == ExperienceLevel.Unknown ? ParseExperienceLevel(scrapedExperience) : parsed.ExperienceLevel;

                            parsed = parsed with
                            {
                                Id = jobId,
                                Description = desc,
                                Title = title,
                                Company = company,
                                Location = location,
                                JobType = jType,
                                ExperienceLevel = exp,
                                PostedAt = scrapedPostedAt ?? parsed.PostedAt,
                                Salary = string.IsNullOrWhiteSpace(parsed.Salary) ? scrapedSalary : parsed.Salary // preserve existing salary if any
                            };
                        }
                    }

                    try { Console.WriteLine($"[DEBUG] Result for {jobId}: Title='{parsed?.Title}', Company='{parsed?.Company}', Loc='{parsed?.Location}', JobType='{parsed?.JobType}', Exp='{parsed?.ExperienceLevel}'"); } catch { }

                    return parsed;
                }
                catch (OperationCanceledException) { throw; }
                catch (PlaywrightException pex)
                {
                    s_logProxyFailed(_logger, attemptProxy, attempt, pex.Message, null);
                    try { await page.DisposeAsync(); } catch { }
                    try { await session.DisposeAsync(); } catch { }
                    continue;
                }
                catch (Exception ex)
                {
                    LinkedInLog.LogFailedToParseJobNode(_logger, ex);
                    return null;
                }
            }
            finally
            {
                try { await page.DisposeAsync(); } catch { }
                if (session is not null)
                {
                    try { await session.DisposeAsync(); } catch { }
                }
            }
        }

        s_logAllProxyFailed(_logger, url, null);
        return null;
    }

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

    private static List<string> ExtractIdsFromSearchHtml(string html)
    {
        var ids = new List<string>();

        // data-entity-urn="urn:li:jobPosting:123"
        foreach (Match m in Regex.Matches(html, "data-entity-urn=\"urn:li:jobPosting:(?<id>[0-9]+)\"", RegexOptions.IgnoreCase))
        {
            var id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }

        // href="/jobs/view/123"
        foreach (Match m in Regex.Matches(html, "/jobs/(?:view|r)/(?<id>[0-9]+)", RegexOptions.IgnoreCase))
        {
            var id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
        }

        // href with query ?jobId=123
        foreach (Match m in Regex.Matches(html, "[?&](?:jobId|id)=(?<id>[0-9]+)", RegexOptions.IgnoreCase))
        {
            var id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
        }

        return ids;
    }
}
