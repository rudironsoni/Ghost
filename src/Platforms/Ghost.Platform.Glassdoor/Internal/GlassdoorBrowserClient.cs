using System.Text.RegularExpressions;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Ghost.Abstractions;
using Ghost.ConsentManagement;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Glassdoor.Internal;

public sealed class GlassdoorBrowserClient : IDisposable
{
    private readonly GhostKernel _kernel;
    private readonly IProxyProvider? _proxyProvider;
    private readonly ILogger<GlassdoorBrowserClient> _logger;
    private readonly IOptions<GlassdoorOptions> _options;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(3);
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> s_logUsingProxy =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(GlassdoorBrowserClient)), "Using proxy: {Proxy}");

    private static readonly Action<ILogger, string, Exception?> s_logNavigating =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, nameof(GlassdoorBrowserClient)), "Navigating to: {Url}");

    private static readonly Action<ILogger, string, Exception?> s_logSessionCreating =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, nameof(SearchAsync)), "Creating browser session. Proxy: {Proxy}");

    private static readonly Action<ILogger, int, Exception?> s_logJobsFound =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(4, nameof(SearchAsync)), "Found {Count} jobs via browser");

    private static readonly Action<ILogger, string, Exception?> s_logProxyFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, nameof(GlassdoorBrowserClient)), "Proxy failed, retrying without proxy: {Message}");

    private static readonly Action<ILogger, Exception?> s_logBrowserFallback =
        LoggerMessage.Define(LogLevel.Information, new EventId(6, nameof(GlassdoorBrowserClient)), "Using browser fallback for Glassdoor");

    private static readonly Action<ILogger, Exception?> s_logConsentDetected =
        LoggerMessage.Define(LogLevel.Debug, new EventId(7, nameof(GlassdoorBrowserClient)), "Consent page detected, attempting to bypass");

    private static readonly Action<ILogger, int, Exception?> s_logBrowserAttemptFailed =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(8, nameof(GlassdoorBrowserClient)), "Browser attempt {Attempt} failed");

    private static readonly Action<ILogger, string, Exception?> s_logClickedConsent =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(9, nameof(GlassdoorBrowserClient)), "Clicked consent button with selector: {Selector}");

    private static readonly Action<ILogger, Exception?> s_logConsentHandleFailed =
        LoggerMessage.Define(LogLevel.Debug, new EventId(10, nameof(GlassdoorBrowserClient)), "Failed to handle consent page");

    private static readonly Action<ILogger, Exception?> s_logDomExtractionFailed =
        LoggerMessage.Define(LogLevel.Debug, new EventId(11, nameof(GlassdoorBrowserClient)), "DOM extraction failed, falling back to regex");

    private static readonly Action<ILogger, Exception?> s_logLoadMoreFailed =
        LoggerMessage.Define(LogLevel.Debug, new EventId(12, nameof(GlassdoorBrowserClient)), "Failed to load more results");

    private readonly ConsentManagerService _consentService;

    public GlassdoorBrowserClient(
        GhostKernel kernel,
        IOptions<GlassdoorOptions> options,
        ILogger<GlassdoorBrowserClient> logger,
        IProxyProvider? proxyProvider = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GlassdoorBrowserClient>.Instance;
        _proxyProvider = proxyProvider;
        _consentService = new ConsentManagerService(null);
    }

    public async Task<IReadOnlyList<JobListing>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        s_logBrowserFallback(_logger, null);

        var jobs = new List<JobListing>();
        var query = Uri.EscapeDataString(criteria.Query ?? string.Empty);
        var location = Uri.EscapeDataString(criteria.Location ?? string.Empty);

        var url = BuildSearchUrl(query, location);

        for (var attempt = 1; attempt <= 3 && jobs.Count < limit; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            IBrowserSession? session = null;
            IPage? page = null;

            try
            {
                await ApplyRateLimitAsync(ct);

                var sessionOptions = await CreateSessionOptionsAsync(ct);
                s_logSessionCreating(_logger, sessionOptions.Proxy?.Server ?? "None", null);

                session = await _kernel.NewSessionAsync(sessionOptions, ct);
                page = await session.NewPageAsync(ct: ct);

                s_logNavigating(_logger, url, null);
                await page.NavigateAsync(url, ct: ct);

                await Task.Delay(2000, ct);

                var html = await page.GetContentAsync(ct);

                if (IsConsentPage(html))
                {
                    s_logConsentDetected(_logger, null);
                    await _consentService.WaitAndHandleConsentAsync(page, maxWaitMs: 8000, checkIntervalMs: 500);
                    html = await page.GetContentAsync(ct);
                }

                var pageJobs = await ExtractJobsFromPageAsync(page, html, ct);

                foreach (var job in pageJobs)
                {
                    if (jobs.Count >= limit) break;
                    jobs.Add(job);
                }

                s_logJobsFound(_logger, jobs.Count, null);

                if (jobs.Count > 0)
                {
                    break;
                }

                if (jobs.Count < limit)
                {
                    var moreJobs = await TryLoadMoreResultsAsync(page, limit - jobs.Count, ct);
                    foreach (var job in moreJobs)
                    {
                        if (jobs.Count >= limit) break;
                        jobs.Add(job);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                s_logBrowserAttemptFailed(_logger, attempt, ex);
                if (attempt >= 3)
                {
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
            finally
            {
                if (page != null)
                {
                    try { await page.DisposeAsync(); } catch { }
                }
                if (session != null)
                {
                    try { await session.DisposeAsync(); } catch { }
                }
            }
        }

        return jobs;
    }

    private static string BuildSearchUrl(string query, string location)
    {
        var baseUrl = "https://www.glassdoor.com/Job/jobs.htm";

        var parameters = new List<string>();

        if (!string.IsNullOrEmpty(query))
        {
            parameters.Add($"sc.keyword={query}");
        }

        if (!string.IsNullOrEmpty(location))
        {
            parameters.Add($"locT=C");
            parameters.Add($"locKeyword={location}");
        }

        parameters.Add("srs=RECENT_SEARCHES");

        return parameters.Count > 0
            ? $"{baseUrl}?{string.Join("&", parameters)}"
            : baseUrl;
    }

    private async Task<SessionOptions> CreateSessionOptionsAsync(CancellationToken ct)
    {
        var options = new SessionOptions();

        if (_options.Value.ProxyEnabled && _proxyProvider != null)
        {
            try
            {
                var proxy = await _proxyProvider.GetProxyAsync("US", ct);
                if (proxy != null)
                {
                    options.Proxy = new SessionOptions.ProxySettings(
                        proxy.Server,
                        proxy.Username,
                        proxy.Password);
                    s_logUsingProxy(_logger, proxy.Server, null);
                }
            }
            catch (Exception ex)
            {
                s_logProxyFailed(_logger, ex.Message, null);
            }
        }

        return options;
    }

    private async Task ApplyRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitSemaphore.WaitAsync(ct);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _rateLimitDelay)
            {
                var waitTime = _rateLimitDelay - timeSinceLastRequest;
                await Task.Delay(waitTime, ct);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private static bool IsConsentPage(string html)
    {
        if (string.IsNullOrEmpty(html))
            return false;

        var consentIndicators = new[]
        {
            "consent",
            "cookie policy",
            "accept cookies",
            "manage cookies",
            "before you continue",
            "privacy policy",
            "terms of use",
            "gdpr",
            "data privacy"
        };

        var lowerHtml = html.ToLowerInvariant();
        return consentIndicators.Any(indicator => lowerHtml.Contains(indicator));
    }

    private async Task HandleConsentPageAsync(IPage page, CancellationToken ct)
    {
        try
        {
            var consentSelectors = new[]
            {
                "button[data-test='accept-cookies-button']",
                "button:has-text('Accept')",
                "button:has-text('Accept All')",
                "button:has-text('I Accept')",
                "button:has-text('Agree')",
                "button:has-text('Continue')",
                "[aria-label*='Accept']",
                "[aria-label*='accept']",
                ".accept-cookies",
                "#accept-cookies",
                "button[class*='accept']",
                "button[class*='consent']",
                "button[id*='accept']",
                "button[id*='consent']"
            };

            foreach (var selector in consentSelectors)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector, ct);
                if (element != null)
                {
                    await element.ClickAsync(ct: ct);
                    s_logClickedConsent(_logger, selector, null);
                    await Task.Delay(1000, ct);
                    return;
                }
                }
                catch { }
            }

            try
            {
                await page.PressAsync("body", "Escape", ct);
                await Task.Delay(500, ct);
            }
            catch { }
        }
        catch (Exception ex)
        {
            s_logConsentHandleFailed(_logger, ex);
        }
    }

    private async Task<List<JobListing>> ExtractJobsFromPageAsync(IPage page, string html, CancellationToken ct)
    {
        var jobs = new List<JobListing>();

        try
        {
            var script = """
                () => {
                    const jobs = [];
                    const jobElements = document.querySelectorAll('[data-test="jobListing"], .jobListing, [data-testid="job-listing"], .job-listing, article[data-job-id]');

                    jobElements.forEach(el => {
                        try {
                            const titleEl = el.querySelector('[data-test="job-title"], .jobTitle, h2 a, .job-title, a[data-test="job-link"]');
                            const companyEl = el.querySelector('[data-test="employer-name"], .employerName, .company-name, .employer');
                            const locationEl = el.querySelector('[data-test="job-location"], .jobLocation, .location');
                            const salaryEl = el.querySelector('[data-test="job-salary"], .salary, .salary-estimate');
                            const linkEl = el.querySelector('a[href*="/job/"], a[data-test="job-title-link"]');

                            const job = {
                                title: titleEl?.textContent?.trim() || '',
                                company: companyEl?.textContent?.trim() || '',
                                location: locationEl?.textContent?.trim() || '',
                                salary: salaryEl?.textContent?.trim() || '',
                                url: linkEl?.href || '',
                                jobId: linkEl?.href?.match(/\/job\/([^\/]+)/)?.[1] || ''
                            };

                            if (job.title && job.company) {
                                jobs.push(job);
                            }
                        } catch (e) {}
                    });

                    return jobs;
                }
            """;

            var extractedJobs = await page.EvaluateAsync<List<Dictionary<string, object>>>(script, null, ct);

            if (extractedJobs != null && extractedJobs.Count > 0)
            {
                foreach (var jobData in extractedJobs)
                {
                    var job = new JobListing
                    {
                        Id = jobData.TryGetValue("jobId", out var id) ? id?.ToString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString(),
                        Title = jobData.TryGetValue("title", out var title) ? title?.ToString() ?? string.Empty : string.Empty,
                        Company = jobData.TryGetValue("company", out var company) ? company?.ToString() ?? string.Empty : string.Empty,
                        Location = jobData.TryGetValue("location", out var loc) ? loc?.ToString() : null,
                        Salary = jobData.TryGetValue("salary", out var salary) ? salary?.ToString() : null,
                        Url = jobData.TryGetValue("url", out var url) ? url?.ToString() : null,
                        Source = "Glassdoor"
                    };

                    jobs.Add(job);
                }
            }
            else
            {
                jobs = ExtractJobsWithRegex(html);
            }
        }
        catch (Exception ex)
        {
            s_logDomExtractionFailed(_logger, ex);
            jobs = ExtractJobsWithRegex(html);
        }

        return jobs;
    }

    private static List<JobListing> ExtractJobsWithRegex(string html)
    {
        var jobs = new List<JobListing>();

        if (string.IsNullOrEmpty(html))
            return jobs;

        var titlePattern = @"<a[^>]*href=[""']/job/[^""']+[""'][^>]*>([^<]+)</a>";
        var companyPattern = @"data-test=[""']employer-name[""'][^>]*>([^<]+)</";
        var locationPattern = @"data-test=[""']job-location[""'][^>]*>([^<]+)</";

        var titleMatches = Regex.Matches(html, titlePattern, RegexOptions.IgnoreCase);
        var companyMatches = Regex.Matches(html, companyPattern, RegexOptions.IgnoreCase);
        var locationMatches = Regex.Matches(html, locationPattern, RegexOptions.IgnoreCase);

        for (int i = 0; i < titleMatches.Count; i++)
        {
            var title = titleMatches[i].Groups[1].Value.Trim();
            var company = i < companyMatches.Count ? companyMatches[i].Groups[1].Value.Trim() : string.Empty;
            var location = i < locationMatches.Count ? locationMatches[i].Groups[1].Value.Trim() : null;

            if (!string.IsNullOrEmpty(title))
            {
                jobs.Add(new JobListing
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Company = company,
                    Location = location,
                    Source = "Glassdoor"
                });
            }
        }

        return jobs;
    }

    private async Task<List<JobListing>> TryLoadMoreResultsAsync(IPage page, int remaining, CancellationToken ct)
    {
        var jobs = new List<JobListing>();

        try
        {
            var loadMoreSelectors = new[]
            {
                "button[data-test='load-more']",
                "button:has-text('Load More')",
                "button:has-text('Show More')",
                "a[aria-label*='Next']",
                "button[class*='next']",
                "button[class*='loadMore']"
            };

            foreach (var selector in loadMoreSelectors)
            {
                try
                {
                    var element = await page.QuerySelectorAsync(selector, ct);
                    if (element != null)
                    {
                        await element.ClickAsync(ct: ct);
                        await Task.Delay(2000, ct);

                        var html = await page.GetContentAsync(ct);
                        var newJobs = await ExtractJobsFromPageAsync(page, html, ct);

                        foreach (var job in newJobs)
                        {
                            if (jobs.Count >= remaining) break;
                            jobs.Add(job);
                        }

                        break;
                    }
                }
                catch { }
            }

            if (jobs.Count == 0)
            {
                await page.EvaluateAsync<object>("() => window.scrollTo(0, document.body.scrollHeight)", null, ct);
                await Task.Delay(2000, ct);

                var html = await page.GetContentAsync(ct);
                var newJobs = await ExtractJobsFromPageAsync(page, html, ct);

                foreach (var job in newJobs)
                {
                    if (jobs.Count >= remaining) break;
                    jobs.Add(job);
                }
            }
        }
        catch (Exception ex)
        {
            s_logLoadMoreFailed(_logger, ex);
        }

        return jobs;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _rateLimitSemaphore?.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
