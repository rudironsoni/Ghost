using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
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

    private static readonly string[] s_descSelectors = new[]
    {
        ".show-more-less-html__markup",
        ".description__text",
        "#job-details",
        ".job-description",
        ".core-section-container__content"
    };

    private static readonly string[] s_titleSelectors = new[]
    {
        ".top-card-layout__title",
        ".top-card-layout__entity-info h1",
        "h1"
    };

    private static readonly string[] s_companySelectors = new[]
    {
        ".top-card-layout__first-subline .topcard__org-name-link",
        ".top-card-layout__company-url",
        "a[data-tracking-control-name='public_jobs_topcard-org-name']",
        ".job-details-jobs-unified-top-card__company-name",
        ".topcard__org-name-link"
    };

    private static readonly string[] s_locationSelectors = new[]
    {
        ".top-card-layout__first-subline .topcard__flavor:not(.topcard__org-name-link)",
        ".top-card-layout__first-subline .topcard__flavor--bullet",
        ".job-details-jobs-unified-top-card__bullet",
        ".job-search-card__location",
        ".topcard__flavor--bullet"
    };

    private static readonly string[] s_salarySelectors = new[]
    {
        ".main-job-card__salary-info",
        ".job-details-jobs-unified-top-card__salary",
        ".job-details-jobs-unified-top-card__salary-info",
        ".description__job-criteria-item--salary",
        ".description__job-criteria-item:has(span:contains('Salary'))",
        ".salary-range",
        ".salary",
        ".job-criteria__item--salary"
    };

    private static readonly string[] s_postedSelectors = new[]
    {
        "time[datetime]",
        "time",
        ".posted-time-ago__text",
        ".topcard__flavor--metadata time",
        ".job-details-jobs-unified-top-card__posted-date",
        "span.posted-time-ago__text"
    };

    public GuestJobSearch(
        IOptions<LinkedInOptions> options,
        ILogger<GuestJobSearch> logger,
        ICountryDomainProvider countryProvider,
        LinkedInSessionPool sessionPool)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(countryProvider);
        ArgumentNullException.ThrowIfNull(sessionPool);
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GuestJobSearch>.Instance;
        _options = options;
        _countryProvider = countryProvider;
        _sessionPool = sessionPool;
    }

    public async Task<IReadOnlyList<string>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        return await SearchIdsAsync(criteria, limit, ct).ConfigureAwait(false);
    }

    private async Task<List<string>> SearchIdsAsync(JobSearchCriteria criteria, int limit, CancellationToken ct)
    {
        List<string> ids = [];
        string query = Uri.EscapeDataString(criteria.Query ?? string.Empty);
        string locationEncoded = Uri.EscapeDataString(criteria.Location ?? string.Empty);

        for (int offset = 0; ids.Count < limit; offset += 25)
        {
            ct.ThrowIfCancellationRequested();
            string url = BuildSearchUrl(criteria, query, locationEncoded, offset);
            List<string>? found = await TryFetchIdsAsync(url, ct).ConfigureAwait(false);

            if (found is null) return ids;
            if (found.Count == 0) break;

            AddUniqueIds(ids, found, limit);
            if (found.Count < 25) break;
        }

        return ids;
    }

    private string BuildSearchUrl(JobSearchCriteria criteria, string query, string location, int offset)
    {
        string baseUrlDomain = _countryProvider.GetDomain(_options.Value.Country);
        string baseUrl = $"{baseUrlDomain}/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords={query}&location={location}&start={offset}";
        string? tpr = criteria.PostedDate switch
        {
            TimePosted.Past24Hours => "r86400",
            TimePosted.PastWeek => "r604800",
            TimePosted.PastMonth => "r2592000",
            _ => null
        };
        return tpr is not null ? baseUrl + $"&f_TPR={tpr}" : baseUrl;
    }

    private static void AddUniqueIds(List<string> ids, List<string> found, int limit)
    {
        foreach (string id in found)
        {
            if (ids.Count >= limit) break;
            if (!ids.Contains(id)) ids.Add(id);
        }
    }

    private async Task<List<string>?> TryFetchIdsAsync(string url, CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            TryFetchResult result = await TryFetchPageAsync(url, attempt, ct).ConfigureAwait(false);

            if (result.ShouldBreak) return result.Ids;
            if (result.ShouldContinue) continue;
            if (result.ShouldThrow) throw result.Exception!;
        }

        s_logAllSessionAttemptsFailed(_logger, url, null);
        return null;
    }

    private async Task<TryFetchResult> TryFetchPageAsync(string url, int attempt, CancellationToken ct)
    {
        IBrowserSession? session = null;
        IPage? page = null;

        try
        {
            s_logSessionCreating(_logger, _options.Value.WarmUpEnabled, null);
            session = await _sessionPool.AcquireAsync(ct).ConfigureAwait(false);
            page = await session.NewPageAsync(ct: ct).ConfigureAwait(false);

            await NavigateWithWarmupAsync(page, url, ct).ConfigureAwait(false);
            await CheckRateLimitAsync(page, url).ConfigureAwait(false);

            string html = await page.GetContentAsync(ct).ConfigureAwait(false);
            TryFetchResult? validation = ValidateSearchResponse(html);
            if (validation is not null) return validation;

            await SaveSessionStateAsync(session).ConfigureAwait(false);
            List<string> ids = ExtractIdsFromSearchHtml(html);
            return new TryFetchResult { Ids = ids, ShouldBreak = true };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (BrowserServiceUnavailableException)
        {
            throw;
        }
        catch (PlaywrightException pex)
        {
            return HandlePlaywrightException(pex, attempt, url);
        }
        catch (Exception ex)
        {
            s_logGuestSearchFailed(_logger, ex);
            LinkedInLog.LogFailedToParseSearchNode(_logger, ex);
            return new TryFetchResult { Ids = [], ShouldBreak = true };
        }
        finally
        {
            await DisposePageAsync(page).ConfigureAwait(false);
            ReleaseSession(session);
        }
    }

    private async Task NavigateWithWarmupAsync(IPage page, string url, CancellationToken ct)
    {
        s_logNavigating(_logger, url, null);
        if (_options.Value.WarmUpEnabled)
        {
            await WarmUpPageAsync(page, ct).ConfigureAwait(false);
        }
        var navOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
        await page.NavigateAsync(url, navOptions, ct: ct).ConfigureAwait(false);
    }

    private static async Task WarmUpPageAsync(IPage page, CancellationToken ct)
    {
        try
        {
            string warmUpUrl = "https://www.google.com";
            var warmNav = new NavigationOptions { Timeout = 10_000, WaitUntil = WaitUntil.Load };
            await page.NavigateAsync(warmUpUrl, warmNav, ct: ct).ConfigureAwait(false);
        }
        catch { }
    }

    private async Task CheckRateLimitAsync(IPage page, string url)
    {
        try
        {
            await LinkedInRateLimitDetector.CheckAsync(page).ConfigureAwait(false);
            s_logRateLimitPassed(_logger, url, null);
        }
        catch { }
    }

    private TryFetchResult? ValidateSearchResponse(string html)
    {
        if (string.IsNullOrEmpty(html)) return new TryFetchResult { Ids = [], ShouldBreak = true };

        if (html.Contains("429 Too Many Requests", StringComparison.OrdinalIgnoreCase) || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
        {
            LinkedInLogGuest.LogGuestApiThrottled(_logger);
            return new TryFetchResult { Ids = [], ShouldBreak = true };
        }

        return null;
    }

    private TryFetchResult HandlePlaywrightException(PlaywrightException pex, int attempt, string url)
    {
        s_logSessionFailed(_logger, attempt, pex.Message, null);
        if (attempt < 3) return new TryFetchResult { ShouldContinue = true };

        throw new BrowserServiceUnavailableException(
            "Failed to connect to LinkedIn after 3 attempts. Browser automation service may be unavailable.",
            pex);
    }

    private async Task SaveSessionStateAsync(IBrowserSession session)
    {
        if (string.IsNullOrEmpty(_options.Value.StorageStatePath)) return;
        try
        {
            s_logSavingSession(_logger, _options.Value.StorageStatePath, null);
            await session.SaveStorageStateAsync(_options.Value.StorageStatePath).ConfigureAwait(false);
        }
        catch { }
    }

    private static async Task DisposePageAsync(IPage? page)
    {
        if (page is null) return;
        try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
    }

    private void ReleaseSession(IBrowserSession? session)
    {
        if (session is null) return;
        _sessionPool.Release(session);
    }

    private sealed class TryFetchResult
    {
        public List<string>? Ids { get; init; }
        public bool ShouldBreak { get; init; }
        public bool ShouldContinue { get; init; }
        public bool ShouldThrow { get; init; }
        public Exception? Exception { get; init; }
    }

    public async Task<JobListing?> FetchJobDetailsAsync(string jobId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        string domain = _countryProvider.GetDomain(_options.Value.Country);
        string url = $"{domain}/jobs-guest/jobs/api/jobPosting/{jobId}";

        for (int attempt = 1; attempt <= 3; attempt++)
        {
            JobListing? result = await TryFetchJobDetailsWithSessionAsync(jobId, url, attempt, ct).ConfigureAwait(false);
            if (result is not null) return result;
        }

        s_logAllSessionAttemptsFailed(_logger, url, null);
        return null;
    }

    private async Task<JobListing?> TryFetchJobDetailsWithSessionAsync(string jobId, string url, int attempt, CancellationToken ct)
    {
        IBrowserSession? session = null;
        IPage? page = null;

        try
        {
            s_logSessionCreating(_logger, _options.Value.WarmUpEnabled, null);
            session = await _sessionPool.AcquireAsync(ct).ConfigureAwait(false);
            page = await session.NewPageAsync(ct: ct).ConfigureAwait(false);

            return await FetchAndParseJobDetailsAsync(page, session, jobId, url, attempt, ct).ConfigureAwait(false);
        }
        finally
        {
            await DisposePageAsync(page).ConfigureAwait(false);
            ReleaseSession(session);
        }
    }

    private async Task<JobListing?> FetchAndParseJobDetailsAsync(
        IPage page,
        IBrowserSession session,
        string jobId,
        string url,
        int attempt,
        CancellationToken ct)
    {
        try
        {
            string html = await NavigateAndGetContentAsync(page, url, ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(html)) return null;
            if (IsThrottledResponse(html))
            {
                LinkedInLogGuest.LogGuestJobEndpointThrottled(_logger, jobId);
                return null;
            }

            JobListing? parsed = ParseJobListing(html, jobId, url);
            parsed = await EnrichJobListingAsync(page, parsed, jobId, url, html, ct).ConfigureAwait(false);

            await SaveSessionStateAsync(session).ConfigureAwait(false);
            LogDebugResult(jobId, parsed);

            return parsed;
        }
        catch (OperationCanceledException) { throw; }
        catch (BrowserServiceUnavailableException) { throw; }
        catch (PlaywrightException pex) { return HandlePlaywrightExceptionForFetch(pex, attempt); }
        catch (Exception ex)
        {
            LinkedInLog.LogFailedToParseJobNode(_logger, ex);
            return null;
        }
    }

    private static bool IsThrottledResponse(string html)
    {
        return html.Contains("429", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("too many requests", StringComparison.OrdinalIgnoreCase);
    }

    private static JobListing? ParseJobListing(string html, string jobId, string url)
    {
        var extractor = (Ghost.IJsonLdExtractor?)Activator.CreateInstance(
            Type.GetType("Ghost.Utilities.JsonLdExtractor, Ghost.Core") ??
            typeof(Ghost.Utilities.JsonLdExtractor));
        var parser = new JsonLdParser(extractor!);
        return parser.Parse(html, jobId, url);
    }

    private static void LogDebugResult(string jobId, JobListing? parsed)
    {
        try
        {
            Console.WriteLine($"[DEBUG] Result for {jobId}: Title='{parsed?.Title}', Company='{parsed?.Company}', Loc='{parsed?.Location}', JobType='{parsed?.JobType}', Exp='{parsed?.ExperienceLevel}'");
        }
        catch { }
    }

    private async Task<JobListing?> EnrichJobListingAsync(
        IPage page,
        JobListing? parsed,
        string jobId,
        string url,
        string html,
        CancellationToken ct)
    {
        if (IsParseComplete(parsed)) return parsed;

        ScrapedJobData scraped = await ScrapeJobDataAsync(page, html, ct).ConfigureAwait(false);
        return MergeJobListingData(parsed, scraped, jobId, url);
    }

    private static bool IsParseComplete(JobListing? parsed)
    {
        if (parsed is null) return false;
        return !string.IsNullOrEmpty(parsed.Description) &&
               !string.IsNullOrEmpty(parsed.Company) &&
               !string.IsNullOrEmpty(parsed.Location);
    }

    private async Task<ScrapedJobData> ScrapeJobDataAsync(IPage page, string html, CancellationToken ct)
    {
        var data = new ScrapedJobData
        {
            Description = await ScrapeFirstAsync(page, s_descSelectors, ct).ConfigureAwait(false),
            Title = await ScrapeFirstAsync(page, s_titleSelectors, ct).ConfigureAwait(false),
            Company = await ScrapeFirstAsync(page, s_companySelectors, ct).ConfigureAwait(false),
            Location = await ScrapeFirstAsync(page, s_locationSelectors, ct).ConfigureAwait(false),
            Salary = await ScrapeSalaryAsync(page, ct).ConfigureAwait(false),
            PostedAt = await ScrapePostedAtAsync(page, ct).ConfigureAwait(false)
        };

        (data.JobType, data.Experience) = await ScrapeJobCriteriaAsync(page, ct).ConfigureAwait(false);
        ApplyRegexFallbacks(data, html);

        return data;
    }

    private static async Task<string?> ScrapeFirstAsync(IPage page, string[] selectors, CancellationToken ct)
    {
        foreach (string selector in selectors)
        {
            ct.ThrowIfCancellationRequested();
            string? text = await TryGetElementTextAsync(page, selector, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text)) return text.Trim();
        }
        return null;
    }

    private static async Task<string?> TryGetElementTextAsync(IPage page, string selector, CancellationToken ct)
    {
        try
        {
            IElement? element = await page.QuerySelectorAsync(selector, ct).ConfigureAwait(false);
            if (element is null) return null;
            string? text = await element.GetTextContentAsync(ct).ConfigureAwait(false);
            return text;
        }
        catch { return null; }
    }

    private static async Task<string?> ScrapeSalaryAsync(IPage page, CancellationToken ct)
    {
        foreach (string selector in s_salarySelectors)
        {
            string? text = await TryGetElementTextAsync(page, selector, ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(text)) return CleanSalaryText(text);
        }
        return null;
    }

    private static string CleanSalaryText(string raw)
    {
        string cleaned = System.Text.RegularExpressions.Regex.Replace(raw, "\\s+", " ").Trim();
        return System.Text.RegularExpressions.Regex.Replace(cleaned, "\\s*-\\s*", " - ");
    }

    private static async Task<DateTimeOffset?> ScrapePostedAtAsync(IPage page, CancellationToken ct)
    {
        foreach (string selector in s_postedSelectors)
        {
            DateTimeOffset? result = await TryParsePostedDateAsync(page, selector, ct).ConfigureAwait(false);
            if (result.HasValue) return result;
        }
        return null;
    }

    private static async Task<DateTimeOffset?> TryParsePostedDateAsync(IPage page, string selector, CancellationToken ct)
    {
        try
        {
            IElement? element = await page.QuerySelectorAsync(selector, ct).ConfigureAwait(false);
            if (element is null) return null;

            string? dateAttr = await element.GetAttributeAsync("datetime", ct).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(dateAttr) && DateTimeOffset.TryParse(dateAttr, out DateTimeOffset dto))
            {
                return dto;
            }

            string? text = await element.GetTextContentAsync(ct).ConfigureAwait(false);
            return ParseRelativeDate(text);
        }
        catch { return null; }
    }

    private static DateTimeOffset? ParseRelativeDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateTimeOffset.TryParse(text, out DateTimeOffset parsed)) return parsed;

        Match match = Regex.Match(
            text,
            "(?<n>\\d+)\\s*(minute|minutes|hour|hours|day|days|week|weeks|month|months|year|years)\\s*ago",
            RegexOptions.IgnoreCase);

        if (!match.Success || !int.TryParse(match.Groups["n"].Value, out int number)) return null;

        string unit = match.Groups[2].Value.ToLowerInvariant();
        TimeSpan delta = unit.StartsWith("minute", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromMinutes(number)
            : unit.StartsWith("hour", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromHours(number)
            : unit.StartsWith("day", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromDays(number)
            : unit.StartsWith("week", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromDays(7 * number)
            : unit.StartsWith("month", StringComparison.OrdinalIgnoreCase) ? TimeSpan.FromDays(30 * number)
            : TimeSpan.FromDays(365 * number);

        return DateTimeOffset.UtcNow - delta;
    }

    private static async Task<(string? JobType, string? Experience)> ScrapeJobCriteriaAsync(IPage page, CancellationToken ct)
    {
        string? jobType = null;
        string? experience = null;

        IReadOnlyList<IElement> criteriaList = await page.QuerySelectorAllAsync(
            ".description__job-criteria-list .description__job-criteria-item, .description__job-criteria-list li, .job-details-jobs-unified-top-card__job-insight",
            ct).ConfigureAwait(false);

        foreach (IElement item in criteriaList)
        {
            (string? header, string? value) = await ExtractCriteriaHeaderValueAsync(item, ct).ConfigureAwait(false);
            if (header is null || value is null) continue;

            if (header.Contains("Employment", StringComparison.OrdinalIgnoreCase)) jobType = value;
            else if (header.Contains("Seniority", StringComparison.OrdinalIgnoreCase)) experience = value;
        }

        return (jobType, experience);
    }

    private static async Task<(string? Header, string? Value)> ExtractCriteriaHeaderValueAsync(IElement item, CancellationToken ct)
    {
        try
        {
            string? text = await item.GetTextContentAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrEmpty(text)) return (null, null);

            string[] parts = text.Split(s_newlines, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();

            if (parts.Length < 2) return (null, null);

            return (parts[0], parts[1]);
        }
        catch { return (null, null); }
    }

    private static void ApplyRegexFallbacks(ScrapedJobData data, string html)
    {
        if (string.IsNullOrWhiteSpace(data.Company))
        {
            Match match = Regex.Match(html, "class=\"[^\"]*topcard__org-name-link[^\"]*\">\\s*([^<]+)\\s*<", RegexOptions.IgnoreCase);
            if (match.Success) data.Company = match.Groups[1].Value.Trim();
        }

        if (string.IsNullOrWhiteSpace(data.Location))
        {
            Match match = Regex.Match(html, "class=\"[^\"]*topcard__flavor--bullet[^\"]*\">\\s*([^<]+)\\s*<", RegexOptions.IgnoreCase);
            if (match.Success) data.Location = match.Groups[1].Value.Trim();
        }
    }

    private static JobListing MergeJobListingData(JobListing? existing, ScrapedJobData scraped, string jobId, string url)
    {
        if (existing is null) return CreateJobListingFromScraped(scraped, jobId, url);

        return existing with
        {
            Id = jobId,
            Description = string.IsNullOrWhiteSpace(existing.Description) ? scraped.Description : existing.Description,
            Title = string.IsNullOrWhiteSpace(existing.Title) ? (scraped.Title ?? existing.Title) : existing.Title,
            Company = string.IsNullOrWhiteSpace(existing.Company) ? (scraped.Company ?? existing.Company) : existing.Company,
            Location = string.IsNullOrWhiteSpace(existing.Location) ? scraped.Location : existing.Location,
            JobType = existing.JobType == JobType.Unknown ? ParseJobType(scraped.JobType) : existing.JobType,
            ExperienceLevel = existing.ExperienceLevel == ExperienceLevel.Unknown ? ParseExperienceLevel(scraped.Experience) : existing.ExperienceLevel,
            PostedAt = scraped.PostedAt ?? existing.PostedAt,
            Salary = string.IsNullOrWhiteSpace(existing.Salary) ? scraped.Salary : existing.Salary
        };
    }

    private static JobListing CreateJobListingFromScraped(ScrapedJobData scraped, string jobId, string url)
    {
        return new JobListing
        {
            Id = jobId,
            Description = scraped.Description,
            Title = scraped.Title ?? string.Empty,
            Company = scraped.Company ?? string.Empty,
            Location = scraped.Location,
            Url = url,
            JobType = ParseJobType(scraped.JobType),
            ExperienceLevel = ParseExperienceLevel(scraped.Experience),
            PostedAt = scraped.PostedAt ?? DateTimeOffset.UtcNow,
            Salary = scraped.Salary,
            Source = "LinkedIn"
        };
    }

    private async Task<string> NavigateAndGetContentAsync(IPage page, string url, CancellationToken ct)
    {
        s_logNavigating(_logger, url, null);

        await WarmUpAndNavigateAsync(page, url, ct).ConfigureAwait(false);
        await CheckRateLimitAsync(page, url).ConfigureAwait(false);

        return await page.GetContentAsync(ct).ConfigureAwait(false);
    }

    private async Task WarmUpAndNavigateAsync(IPage page, string url, CancellationToken ct)
    {
        if (_options.Value.WarmUpEnabled) await WarmUpPageAsync(page, ct).ConfigureAwait(false);

        var navigationOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
        await page.NavigateAsync(url, navigationOptions, ct: ct).ConfigureAwait(false);
    }

    private JobListing? HandlePlaywrightExceptionForFetch(PlaywrightException exception, int attempt)
    {
        s_logSessionFailed(_logger, attempt, exception.Message, null);
        if (attempt < 3) return null;

        throw new BrowserServiceUnavailableException(
            "Failed to fetch job details after 3 attempts. Browser automation service may be unavailable.",
            exception);
    }

    private static JobType ParseJobType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return JobType.Unknown;
        string normalized = type.ToUpperInvariant().Replace("_", "").Replace("-", "").Replace(" ", "");

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

        if (n.Contains("intern")) return ExperienceLevel.EntryLevel;
        if (n.Contains("entry")) return ExperienceLevel.EntryLevel;
        if (n.Contains("associate")) return ExperienceLevel.MidLevel;
        if (n.Contains("mid")) return ExperienceLevel.MidLevel;
        if (n.Contains("senior")) return ExperienceLevel.Senior;
        if (n.Contains("director") || n.Contains("manager") || n.Contains("executive")) return ExperienceLevel.Manager;

        return ExperienceLevel.Unknown;
    }

    private static List<string> ExtractIdsFromSearchHtml(string html)
    {
        List<string> ids = [];

        foreach (Match m in Regex.Matches(html, "data-entity-urn=\"urn:li:jobPosting:(?<id>[0-9]+)\"", RegexOptions.IgnoreCase))
        {
            string id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id)) ids.Add(id);
        }

        foreach (Match m in Regex.Matches(html, "/jobs/(?:view|r)/(?<id>[0-9]+)", RegexOptions.IgnoreCase))
        {
            string id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
        }

        foreach (Match m in Regex.Matches(html, "[?&](?:jobId|id)=(?<id>[0-9]+)", RegexOptions.IgnoreCase))
        {
            string id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && !ids.Contains(id)) ids.Add(id);
        }

        return ids;
    }

    private sealed class ScrapedJobData
    {
        public string? Description { get; set; }
        public string? Title { get; set; }
        public string? Company { get; set; }
        public string? Location { get; set; }
        public string? JobType { get; set; }
        public string? Experience { get; set; }
        public string? Salary { get; set; }
        public DateTimeOffset? PostedAt { get; set; }
    }
}
