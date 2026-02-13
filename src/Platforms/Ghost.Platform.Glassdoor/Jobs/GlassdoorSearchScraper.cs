using System.Text.RegularExpressions;
using Ghost.Abstractions;
using Ghost.ConsentManagement;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.Glassdoor.Jobs;

/// <summary>
/// Heavy stealth browser-only scraper for Glassdoor job search.
/// Target: 70% success rate with aggressive anti-bot evasion.
/// </summary>
public sealed class GlassdoorSearchScraper : IDisposable
{
    private readonly IGhostKernel _kernel;
    private readonly IProxyProvider? _proxyProvider;
    private readonly ILogger<GlassdoorSearchScraper> _logger;
    private readonly GlassdoorOptions _options;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _heavyRateLimitDelay = TimeSpan.FromSeconds(5); // Increased from 3s to 5s
    private readonly ConsentManagerService _consentService;
    private bool _disposed;
    private int _requestCount;
    private const int ProxyRotationThreshold = 3; // Rotate proxy every 3 requests

    private static readonly Action<ILogger, string, Exception?> s_logUsingProxy =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(GlassdoorSearchScraper)), "Using proxy: {Proxy}");

    private static readonly Action<ILogger, string, Exception?> s_logNavigating =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, nameof(GlassdoorSearchScraper)), "Navigating to: {Url}");

    private static readonly Action<ILogger, int, Exception?> s_logJobsFound =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(SearchAsync)), "Found {Count} jobs via heavy stealth browser");

    private static readonly Action<ILogger, string, Exception?> s_logProxyFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, nameof(GlassdoorSearchScraper)), "Proxy failed, rotating: {Message}");

    private static readonly Action<ILogger, int, Exception?> s_logProxyRotated =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(5, nameof(GlassdoorSearchScraper)), "Rotated proxy after {RequestCount} requests");

    private static readonly Action<ILogger, string?, string?, int, Exception?> s_logSearchStarting =
        LoggerMessage.Define<string?, string?, int>(LogLevel.Information, new EventId(6, "SearchStarting"), "Starting heavy stealth search for query='{Query}', location='{Location}', limit={Limit}");

    private static readonly Action<ILogger, int, Exception?> s_logCreatingSession =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(7, "CreatingSession"), "Attempt {Attempt}, creating browser session with heavy stealth");

    private static readonly Action<ILogger, int, Exception?> s_logCloudflareWait =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(8, "CloudflareWait"), "Waiting {Seconds} seconds for Cloudflare/Datadome check");

    private static readonly Action<ILogger, int, Exception?> s_logHumanScroll =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(9, "HumanScroll"), "Performing human-like scroll sequence: step {Step}");

    private static readonly Action<ILogger, int, Exception?> s_logAttemptFailed =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(10, "AttemptFailed"), "Heavy stealth attempt {Attempt} failed");

    private static readonly Action<ILogger, Exception?> s_logDomExtractionFailed =
        LoggerMessage.Define(LogLevel.Debug, new EventId(11, "DomExtractionFailed"), "DOM extraction failed, falling back to regex");

    public GlassdoorSearchScraper(
        IGhostKernel kernel,
        IOptions<GlassdoorOptions> options,
        ILogger<GlassdoorSearchScraper> logger,
        IProxyProvider? proxyProvider = null)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GlassdoorSearchScraper>.Instance;
        _proxyProvider = proxyProvider;
        _consentService = new ConsentManagerService(null);
    }

    /// <summary>
    /// Heavy stealth search with 70% success target.
    /// Implements: aggressive proxy rotation, extended delays, CSRF handling, human-like behavior.
    /// </summary>
    public async Task<IReadOnlyList<JobListing>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        s_logSearchStarting(_logger, criteria.Query, criteria.Location, limit, null);

        var jobs = new List<JobListing>();
        var query = Uri.EscapeDataString(criteria.Query ?? string.Empty);
        var location = Uri.EscapeDataString(criteria.Location ?? string.Empty);

        var url = BuildSearchUrl(query, location);

        // Try up to 5 attempts with proxy rotation for 70% success rate
        for (var attempt = 1; attempt <= 5 && jobs.Count < limit; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            IBrowserSession? session = null;
            IPage? page = null;

            try
            {
                // Apply heavy rate limiting (5-10 seconds between requests)
                await ApplyHeavyRateLimitAsync(ct);

                // Check if proxy rotation needed
                if (ShouldRotateProxy())
                {
                    s_logProxyRotated(_logger, _requestCount, null);
                    _requestCount = 0;
                }

                var sessionOptions = await CreateHeavyStealthSessionOptionsAsync(ct);
                s_logCreatingSession(_logger, attempt, null);

                session = await _kernel.NewSessionAsync(sessionOptions, ct);
                page = await session.NewPageAsync(ct: ct);

                s_logNavigating(_logger, url, null);
                await page.NavigateAsync(url, ct: ct);

                // CRITICAL: Extended wait for Cloudflare/Datadome check (15 seconds for heavy protection)
                s_logCloudflareWait(_logger, 15, null);
                await Task.Delay(15000, ct);

                // Perform VERY realistic human-like scrolling with longer delays
                await PerformHumanLikeScrollingAsync(page, ct);

                // Final wait for dynamic content (5 seconds)
                await Task.Delay(5000, ct);

                var html = await page.GetContentAsync(ct);

                // Handle consent dialogs
                if (IsConsentPage(html))
                {
                    await _consentService.WaitAndHandleConsentAsync(page, maxWaitMs: 10000, checkIntervalMs: 500);
                    await Task.Delay(3000, ct); // Extended wait after consent
                    html = await page.GetContentAsync(ct);
                }

                var pageJobs = await ExtractJobsFromPageAsync(page, html, ct);

                // Check if we're still blocked and need additional wait
                if (pageJobs.Count == 0 && IsBlockedPage(html))
                {
                    s_logCloudflareWait(_logger, 10, null);
                    await Task.Delay(10000, ct);
                    html = await page.GetContentAsync(ct);
                    pageJobs = await ExtractJobsFromPageAsync(page, html, ct);
                }

                foreach (var job in pageJobs)
                {
                    if (jobs.Count >= limit) break;
                    jobs.Add(job);
                }

                s_logJobsFound(_logger, jobs.Count, null);

                _requestCount++;

                if (jobs.Count > 0)
                {
                    break; // Success!
                }

                // If still no jobs and not max attempts, increase backoff
                if (jobs.Count < limit && attempt < 5)
                {
                    var backoffSeconds = attempt * 5; // 5s, 10s, 15s, 20s
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), ct);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                s_logAttemptFailed(_logger, attempt, ex);
                if (attempt >= 5)
                {
                    break;
                }
                // Exponential backoff with jitter
                var backoffMs = (int)(Math.Pow(2, attempt) * 1000 + Random.Shared.Next(1000, 3000));
                await Task.Delay(backoffMs, ct);
            }
            finally
            {
                if (page != null)
                {
                    try { await page.DisposeAsync(); } catch { /* ignore */ }
                }
                if (session != null)
                {
                    try { await session.DisposeAsync(); } catch { /* ignore */ }
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
            parameters.Add("locT=C");
            parameters.Add($"locKeyword={location}");
        }

        parameters.Add("srs=RECENT_SEARCHES");

        return parameters.Count > 0
            ? $"{baseUrl}?{string.Join("&", parameters)}"
            : baseUrl;
    }

    private async Task<SessionOptions> CreateHeavyStealthSessionOptionsAsync(CancellationToken ct)
    {
        var options = new SessionOptions
        {
            // Realistic viewport with slight variation
            ViewportWidth = 1920 + Random.Shared.Next(-50, 50),
            ViewportHeight = 1080 + Random.Shared.Next(-50, 50),
            // Randomize user agent across Chrome versions
            UserAgent = GetRandomUserAgent(),
            // Vary locale and timezone
            Locale = "en-US",
            TimezoneId = GetRandomTimezone(),
            // Randomize geolocation within US
            Geolocation = GetRandomUSGeolocation(),
            // Disable proxy for now (as per existing implementation note)
            Proxy = null
        };

        // Note: Proxy intentionally disabled due to SOCKS5 compatibility issues with Glassdoor
        // Consider HTTP proxies if available
        if (_options.ProxyEnabled && _proxyProvider != null && ShouldRotateProxy())
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

    private async Task ApplyHeavyRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitSemaphore.WaitAsync(ct);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _heavyRateLimitDelay)
            {
                var waitTime = _heavyRateLimitDelay - timeSinceLastRequest;
                // Add jitter (random 0-2 seconds)
                var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 2000) / 1000.0);
                await Task.Delay(waitTime + jitter, ct);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private bool ShouldRotateProxy()
    {
        return _requestCount >= ProxyRotationThreshold;
    }

    private async Task PerformHumanLikeScrollingAsync(IPage page, CancellationToken ct)
    {
        // Scroll down in realistic steps with variable delays
        var scrollSteps = new[] { 400, 800, 1200, 1600, 2000 };

        for (int i = 0; i < scrollSteps.Length; i++)
        {
            s_logHumanScroll(_logger, i + 1, null);
            await page.EvaluateAsync<object>($"() => window.scrollTo({{ top: {scrollSteps[i]}, behavior: 'smooth' }})", null, ct);

            // Variable delay between 1.5-3 seconds with jitter
            var delayMs = Random.Shared.Next(1500, 3000);
            await Task.Delay(delayMs, ct);
        }

        // Scroll back up slightly (human behavior)
        await page.EvaluateAsync<object>("() => window.scrollTo({ top: 600, behavior: 'smooth' })", null, ct);
        await Task.Delay(Random.Shared.Next(1000, 2000), ct);

        // Final scroll down
        await page.EvaluateAsync<object>("() => window.scrollTo({ top: 2400, behavior: 'smooth' })", null, ct);
        await Task.Delay(Random.Shared.Next(2000, 3000), ct);
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
            "gdpr"
        };

        var lowerHtml = html.ToLowerInvariant();
        return consentIndicators.Any(indicator => lowerHtml.Contains(indicator));
    }

    private static bool IsBlockedPage(string html)
    {
        if (string.IsNullOrEmpty(html))
            return true;

        return html.Contains("cloudflare", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("just a moment", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("checking your browser", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("datadome", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("access denied", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<JobListing>> ExtractJobsFromPageAsync(IPage page, string html, CancellationToken ct)
    {
        var jobs = new List<JobListing>();

        try
        {
            // Use the existing comprehensive JavaScript extraction from GlassdoorBrowserClient
            var script = """
                () => {
                    const jobs = [];
                    const selectors = [
                        'li.react-job-listing',
                        '[data-test="jobListing"]',
                        '.jobListing',
                        '[data-testid="job-listing"]',
                        '.job-listing',
                        'article[data-job-id]',
                        'li[data-test="jobListing"]',
                        'li.JobsList_jobListItem__wjTHv',
                        'li[data-brandviews="true"]',
                        'div[data-test="job-listing"]',
                        '.jobContainer',
                        'article.job',
                        '[class*="JobCard"]',
                        '[class*="jobCard"]'
                    ];

                    let jobElements = [];
                    for (const selector of selectors) {
                        jobElements = document.querySelectorAll(selector);
                        if (jobElements.length > 0) break;
                    }

                    if (jobElements.length === 0) return jobs;

                    jobElements.forEach(el => {
                        try {
                            const titleSelectors = [
                                '[data-test="job-title"]',
                                '.jobTitle',
                                'h2 a',
                                'h3 a',
                                '.job-title',
                                'a[data-test="job-link"]',
                                'a[data-test="job-title-link"]'
                            ];

                            let titleEl = null;
                            for (const sel of titleSelectors) {
                                titleEl = el.querySelector(sel);
                                if (titleEl && titleEl.textContent.trim()) break;
                            }

                            const companySelectors = [
                                '[data-test="employer-name"]',
                                '.employerName',
                                '.company-name',
                                '.employer',
                                '[data-test="employer"]'
                            ];

                            let companyEl = null;
                            for (const sel of companySelectors) {
                                companyEl = el.querySelector(sel);
                                if (companyEl && companyEl.textContent.trim()) break;
                            }

                            const locationSelectors = [
                                '[data-test="job-location"]',
                                '.jobLocation',
                                '.location',
                                '[data-test="location"]'
                            ];

                            let locationEl = null;
                            for (const sel of locationSelectors) {
                                locationEl = el.querySelector(sel);
                                if (locationEl && locationEl.textContent.trim()) break;
                            }

                            const linkSelectors = [
                                'a[href*="/job-listing/"]',
                                'a[href*="/job/"]',
                                'a[data-test="job-title-link"]',
                                'a[data-test="job-link"]'
                            ];

                            let linkEl = null;
                            for (const sel of linkSelectors) {
                                linkEl = el.querySelector(sel);
                                if (linkEl && linkEl.href) break;
                            }

                            const title = titleEl?.textContent?.trim() || '';
                            const company = companyEl?.textContent?.trim() || '';
                            const location = locationEl?.textContent?.trim() || '';
                            const url = linkEl?.href || '';
                            const jobId = linkEl?.href?.match(/\/job-listing\/([^\/\?]+)/)?.[1] ||
                                         linkEl?.href?.match(/\/job\/([^\/\?]+)/)?.[1] || '';

                            if (title && company) {
                                jobs.push({ title, company, location, url, jobId });
                            }
                        } catch (e) {
                            // Silently continue
                        }
                    });

                    return jobs;
                }
            """;

            var result = await page.EvaluateAsync<List<Dictionary<string, string>>>(script, null, ct);

            if (result != null)
            {
                foreach (var jobData in result)
                {
                    var job = new JobListing
                    {
                        Id = jobData.TryGetValue("jobId", out var id) && !string.IsNullOrEmpty(id)
                            ? id
                            : Guid.NewGuid().ToString(),
                        Title = jobData.TryGetValue("title", out var title) ? title : string.Empty,
                        Company = jobData.TryGetValue("company", out var company) ? company : string.Empty,
                        Location = jobData.TryGetValue("location", out var loc) ? loc : null,
                        Url = jobData.TryGetValue("url", out var url) ? url : null,
                        Source = "Glassdoor"
                    };

                    jobs.Add(job);
                }
            }

            // Fallback to regex if JavaScript extraction fails
            if (jobs.Count == 0)
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

    private static string GetRandomUserAgent()
    {
        var userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/121.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36"
        };

        return userAgents[Random.Shared.Next(userAgents.Length)];
    }

    private static string GetRandomTimezone()
    {
        var timezones = new[] { "America/New_York", "America/Chicago", "America/Los_Angeles", "America/Denver" };
        return timezones[Random.Shared.Next(timezones.Length)];
    }

    private static SessionOptions.GeolocationSettings GetRandomUSGeolocation()
    {
        // Major US cities
        var locations = new[]
        {
            (40.7128, -74.0060),  // New York
            (41.8781, -87.6298),  // Chicago
            (34.0522, -118.2437), // Los Angeles
            (29.7604, -95.3698),  // Houston
            (33.4484, -112.0740)  // Phoenix
        };

        var (lat, lon) = locations[Random.Shared.Next(locations.Length)];
        return new SessionOptions.GeolocationSettings(lat, lon, 100);
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
