using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace Ghost.Plugin.LinkedIn.Internal;

public sealed class GuestJobSearch : IGuestJobSearch
{
    private readonly ILogger<GuestJobSearch> _logger;
    private readonly IOptions<LinkedInOptions> _options;
    private readonly ICountryDomainProvider _countryProvider;
    private readonly LinkedInSessionPool _sessionPool;

    private static readonly Action<ILogger, string, Exception?> s_logNavigating =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, nameof(GuestJobSearch)), "Navigating to: {Url}");

    private static readonly Action<ILogger, bool, Exception?> s_logSessionCreating =
        LoggerMessage.Define<bool>(LogLevel.Information, new EventId(5, nameof(SearchAsync)), "Creating pooled session. Warm-up: {WarmUp}");

    private static readonly Action<ILogger, string, Exception?> s_logRateLimitPassed =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, nameof(SearchAsync)), "Rate limit check passed for {Url}");

    private static readonly Action<ILogger, string, Exception?> s_logSavingSession =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(7, nameof(SearchAsync)), "Saving session state to {Path}");

    private static readonly Action<ILogger, Exception?> s_logGuestSearchFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(GuestJobSearch)), "Guest search navigation/parsing failed");

    private static readonly Action<ILogger, int, string, Exception?> s_logSessionFailed =
        LoggerMessage.Define<int, string>(LogLevel.Warning, new EventId(8, nameof(GuestJobSearch)), "Session failed (Attempt {Attempt}/3). Error: {Message}");

    private static readonly Action<ILogger, string, Exception?> s_logAllSessionAttemptsFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(9, nameof(GuestJobSearch)), "All session attempts failed for {Url}");

    private static readonly char[] s_newlines = { '\n', '\r' };

    public GuestJobSearch(
        IOptions<LinkedInOptions> options,
        ILogger<GuestJobSearch> logger,
        ICountryDomainProvider countryProvider,
        LinkedInSessionPool sessionPool)
    {
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GuestJobSearch>.Instance;
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _countryProvider = countryProvider ?? throw new ArgumentNullException(nameof(countryProvider));
        _sessionPool = sessionPool ?? throw new ArgumentNullException(nameof(sessionPool));
    }

    public async Task<IReadOnlyList<string>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var ids = new List<string>();

        string q = Uri.EscapeDataString(criteria.Query ?? string.Empty);
        string loc = Uri.EscapeDataString(criteria.Location ?? string.Empty);

        for (int offset = 0; ids.Count < limit; offset += 25)
        {
            ct.ThrowIfCancellationRequested();

            // Build base URL and append time filter if present
            string baseUrlDomain = _countryProvider.GetDomain(_options.Value.Country);
            string baseUrl = $"{baseUrlDomain}/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords={q}&location={loc}&start={offset}";
            string? tpr = criteria.PostedDate switch
            {
                TimePosted.Past24Hours => "r86400",
                TimePosted.PastWeek => "r604800",
                TimePosted.PastMonth => "r2592000",
                _ => null
            };
            string url = tpr is not null ? baseUrl + $"&f_TPR={tpr}" : baseUrl;

            List<string>? found = null;
            bool success = false;

            // Try up to 3 attempts, fetching a fresh proxy/session each time
            for (int attempt = 1; attempt <= 3 && !success; attempt++)
            {
                IBrowserSession? session = null;
                IPage? page = null;

                try
                {
                    s_logSessionCreating(_logger, _options.Value.WarmUpEnabled, null);
                    session = await _sessionPool.AcquireAsync(ct).ConfigureAwait(false);
                    page = await session.NewPageAsync(ct: ct).ConfigureAwait(false);

                    s_logNavigating(_logger, url, null);
                    if (_options.Value.WarmUpEnabled)
                    {
                        try
                        {
                            // Simple warm-up: visit a safe URL first
                            string warmUpUrl = "https://www.google.com";
                            var warmNav = new NavigationOptions { Timeout = 10_000, WaitUntil = WaitUntil.Load };
                            await page.NavigateAsync(warmUpUrl, warmNav, ct: ct).ConfigureAwait(false);
                        }
                        catch { }
                    }

                    var navOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
                    await page.NavigateAsync(url, navOptions, ct: ct).ConfigureAwait(false);

                    try
                    {
                        await LinkedInRateLimitDetector.CheckAsync(page).ConfigureAwait(false);
                        s_logRateLimitPassed(_logger, url, null);
                    }
                    catch { }

                    string html = await page.GetContentAsync(ct).ConfigureAwait(false);
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
                        try { s_logSavingSession(_logger, _options.Value.StorageStatePath, null); await session.SaveStorageStateAsync(_options.Value.StorageStatePath).ConfigureAwait(false); } catch { }
                    }

                    foreach (string id in found)
                    {
                        if (ids.Count >= limit) break;
                        if (!ids.Contains(id)) ids.Add(id);
                    }

                    success = true;
                }
                catch (OperationCanceledException) { throw; }
                catch (BrowserServiceUnavailableException)
                {
                    // Browser service is unavailable - propagate to caller
                    throw;
                }
                catch (PlaywrightException pex)
                {
                    // Any Playwright error during navigation/setup should trigger a proxy retry.
                    s_logSessionFailed(_logger, attempt, pex.Message, null);
                    if (attempt == 3)
                    {
                        // All retries exhausted - wrap and throw
                        throw new BrowserServiceUnavailableException(
                            "Failed to connect to LinkedIn after 3 attempts. Browser automation service may be unavailable.",
                            pex);
                    }
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
                    if (page != null)
                    {
                        try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
                    }
                    if (session != null)
                    {
                        _sessionPool.Release(session);
                    }
                }
            }

            if (!success)
            {
                s_logAllSessionAttemptsFailed(_logger, url, null);
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
        string domain = _countryProvider.GetDomain(_options.Value.Country);
        string url = $"{domain}/jobs-guest/jobs/api/jobPosting/{jobId}";
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            IBrowserSession? session = null;
            IPage? page = null;

            try
            {
                s_logSessionCreating(_logger, _options.Value.WarmUpEnabled, null);
                session = await _sessionPool.AcquireAsync(ct).ConfigureAwait(false);
                page = await session.NewPageAsync(ct: ct).ConfigureAwait(false);

                try
                {
                    s_logNavigating(_logger, url, null);
                    if (_options.Value.WarmUpEnabled)
                    {
                        try
                        {
                            // Simple warm-up: visit a safe URL first
                            string warmUpUrl = "https://www.google.com";
                            var warmNav = new NavigationOptions { Timeout = 10_000, WaitUntil = WaitUntil.Load };
                            await page.NavigateAsync(warmUpUrl, warmNav, ct: ct).ConfigureAwait(false);
                        }
                        catch { }
                    }

                    var navOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
                    await page.NavigateAsync(url, navOptions, ct: ct).ConfigureAwait(false);
                    try { await LinkedInRateLimitDetector.CheckAsync(page).ConfigureAwait(false); } catch { }
                    Console.WriteLine($"[DEBUG] Fetching content for {jobId}...");
                    string html = await page.GetContentAsync(ct).ConfigureAwait(false);

                    // NOTE: debug artifacts removed - production code should not write files during parsing
                    if (string.IsNullOrEmpty(html)) return null;

                    if (html.Contains("429", StringComparison.OrdinalIgnoreCase) || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                    {
                        LinkedInLogGuest.LogGuestJobEndpointThrottled(_logger, jobId);
                        return null;
                    }

                    // Use the JsonLdExtractor implementation from Ghost.Utilities via DI/Activator
                    var extractor = (Ghost.Abstractions.IJsonLdExtractor?)Activator.CreateInstance(Type.GetType("Ghost.Utilities.JsonLdExtractor, Ghost.Core") ?? typeof(Ghost.Utilities.JsonLdExtractor));
                    var parser = new JsonLdParser(extractor!);
                    JobListing? parsed = parser.Parse(html, jobId, url);

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
                            foreach (string sel in selectors)
                            {
                                ct.ThrowIfCancellationRequested();
                                try
                                {
                                    IElement? handle = await p.QuerySelectorAsync(sel, ct).ConfigureAwait(false);
                                    if (handle is null) continue;
                                    string? txt = await handle.GetTextContentAsync(ct).ConfigureAwait(false);
                                    if (!string.IsNullOrWhiteSpace(txt)) return txt?.Trim();
                                }
                                catch { }
                            }
                            return null;
                        }

                        // Robust selectors for guest view (updated 2026)
                        string[] descSelectors = new[] {
                            ".show-more-less-html__markup",
                            ".description__text",
                            "#job-details",
                            ".job-description",
                            ".core-section-container__content"
                        };

                        string[] titleSelectors = new[] {
                            ".top-card-layout__title",
                            ".top-card-layout__entity-info h1",
                            "h1"
                        };

                        string[] companySelectors = new[] {
                            ".top-card-layout__first-subline .topcard__org-name-link",
                            ".top-card-layout__company-url",
                            "a[data-tracking-control-name='public_jobs_topcard-org-name']",
                            ".job-details-jobs-unified-top-card__company-name",
                            ".topcard__org-name-link"
                        };

                        string[] locationSelectors = new[] {
                            ".top-card-layout__first-subline .topcard__flavor:not(.topcard__org-name-link)",
                            ".top-card-layout__first-subline .topcard__flavor--bullet",
                            ".job-details-jobs-unified-top-card__bullet",
                            ".job-search-card__location",
                            ".topcard__flavor--bullet"
                        };

                        string? scrapedDescription = await ScrapeFirstAsync(page, descSelectors, ct).ConfigureAwait(false);
                        string? scrapedTitle = await ScrapeFirstAsync(page, titleSelectors, ct).ConfigureAwait(false);
                        string? scrapedCompany = await ScrapeFirstAsync(page, companySelectors, ct).ConfigureAwait(false);
                        string? scrapedLocation = await ScrapeFirstAsync(page, locationSelectors, ct).ConfigureAwait(false);

                        // Try to scrape criteria for JobType/Experience
                        string? scrapedJobType = null;
                        string? scrapedExperience = null;

                        // Prefer the newer criteria item structure
                        IReadOnlyList<IElement> criteriaList = await page.QuerySelectorAllAsync(".description__job-criteria-list .description__job-criteria-item, .description__job-criteria-list li, .job-details-jobs-unified-top-card__job-insight", ct).ConfigureAwait(false);
                        foreach (IElement item in criteriaList)
                        {
                            try
                            {
                                string? text = await item.GetTextContentAsync(ct).ConfigureAwait(false);
                                if (!string.IsNullOrEmpty(text))
                                {
                                    // Normalize and split lines - header on first line, value on second
                                    string[] parts = text.Split(s_newlines, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
                                    if (parts.Length >= 2)
                                    {
                                        string header = parts[0];
                                        string value = parts[1];
                                        if (header.Contains("Employment", StringComparison.OrdinalIgnoreCase) || header.Contains("Employment type", StringComparison.OrdinalIgnoreCase)) scrapedJobType = value;
                                        else if (header.Contains("Seniority", StringComparison.OrdinalIgnoreCase) || header.Contains("Seniority level", StringComparison.OrdinalIgnoreCase)) scrapedExperience = value;
                                    }
                                }
                            }
                            catch { }
                        }

                        // Salary: attempt to find salary block in guest view using multiple selectors
                        string? scrapedSalary = null;
                        try
                        {
                            string[] salarySelectors = new[] {
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
                            foreach (string? sel in salarySelectors)
                            {
                                try
                                {
                                    IElement? el = await page.QuerySelectorAsync(sel, ct).ConfigureAwait(false);
                                    if (el is null) continue;
                                    string? raw = await el.GetTextContentAsync(ct).ConfigureAwait(false);
                                    if (string.IsNullOrWhiteSpace(raw)) continue;
                                    string cleaned = System.Text.RegularExpressions.Regex.Replace(raw, "\\s+", " ").Trim();
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
                            string[] postedSelectors = new[] {
                                "time[datetime]",
                                "time",
                                ".posted-time-ago__text",
                                ".topcard__flavor--metadata time",
                                ".job-details-jobs-unified-top-card__posted-date",
                                "span.posted-time-ago__text"
                            };

                            foreach (string? sel in postedSelectors)
                            {
                                try
                                {
                                    IElement? el = await page.QuerySelectorAsync(sel, ct).ConfigureAwait(false);
                                    if (el is null) continue;
                                    string? dtAttr = await el.GetAttributeAsync("datetime", ct).ConfigureAwait(false);
                                    if (!string.IsNullOrWhiteSpace(dtAttr) && DateTimeOffset.TryParse(dtAttr, out DateTimeOffset dto))
                                    {
                                        scrapedPostedAt = dto;
                                        break;
                                    }

                                    string? txt = await el.GetTextContentAsync(ct).ConfigureAwait(false);
                                    if (string.IsNullOrWhiteSpace(txt)) continue;

                                    // Try absolute parse first
                                    if (DateTimeOffset.TryParse(txt, out DateTimeOffset dtParsed))
                                    {
                                        scrapedPostedAt = dtParsed;
                                        break;
                                    }

                                    // Try relative times like '3 days ago' or 'Posted 4 hours ago'
                                    Match m = Regex.Match(txt, "(?<n>\\d+)\\s*(minute|minutes|hour|hours|day|days|week|weeks|month|months|year|years)\\s*ago", RegexOptions.IgnoreCase);
                                    if (m.Success && int.TryParse(m.Groups["n"].Value, out int n))
                                    {
                                        string unit = m.Groups[2].Value.ToLowerInvariant();
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
                            Match m = Regex.Match(html, "class=\"[^\"]*topcard__org-name-link[^\"]*\">\\s*([^<]+)\\s*<", RegexOptions.IgnoreCase);
                            if (m.Success) scrapedCompany = m.Groups[1].Value.Trim();
                        }
                        if (string.IsNullOrWhiteSpace(scrapedLocation))
                        {
                            Match m = Regex.Match(html, "class=\"[^\"]*topcard__flavor--bullet[^\"]*\">\\s*([^<]+)\\s*<", RegexOptions.IgnoreCase);
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
                            string? desc = string.IsNullOrWhiteSpace(parsed.Description) ? scrapedDescription : parsed.Description;
                            string title = string.IsNullOrWhiteSpace(parsed.Title) ? (scrapedTitle ?? parsed.Title) : parsed.Title;
                            string company = string.IsNullOrWhiteSpace(parsed.Company) ? (scrapedCompany ?? parsed.Company) : parsed.Company;
                            string? location = string.IsNullOrWhiteSpace(parsed.Location) ? scrapedLocation : parsed.Location;
                            JobType jType = parsed.JobType == JobType.Unknown ? ParseJobType(scrapedJobType) : parsed.JobType;
                            ExperienceLevel exp = parsed.ExperienceLevel == ExperienceLevel.Unknown ? ParseExperienceLevel(scrapedExperience) : parsed.ExperienceLevel;

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
                catch (BrowserServiceUnavailableException)
                {
                    // Browser service is unavailable - propagate to caller
                    throw;
                }
                catch (PlaywrightException pex)
                {
                    s_logSessionFailed(_logger, attempt, pex.Message, null);
                    if (attempt == 3)
                    {
                        // All retries exhausted - wrap and throw
                        throw new BrowserServiceUnavailableException(
                            "Failed to fetch job details after 3 attempts. Browser automation service may be unavailable.",
                            pex);
                    }
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
                if (page != null)
                {
                    try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
                }
                if (session != null)
                {
                    _sessionPool.Release(session);
                }
            }
        }

        s_logAllSessionAttemptsFailed(_logger, url, null);
        return null;
    }

    private static JobType ParseJobType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return JobType.Unknown;
        string normalized = type.ToUpperInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");
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
        string n = level.Trim().ToLowerInvariant();
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
            string id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }

        // href="/jobs/view/123"
        foreach (Match m in Regex.Matches(html, "/jobs/(?:view|r)/(?<id>[0-9]+)", RegexOptions.IgnoreCase))
        {
            string id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
        }

        // href with query ?jobId=123
        foreach (Match m in Regex.Matches(html, "[?&](?:jobId|id)=(?<id>[0-9]+)", RegexOptions.IgnoreCase))
        {
            string id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
        }

        return ids;
    }
}
