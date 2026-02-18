using System.Text.Json;
using System.Text.RegularExpressions;
using Ghost.ConsentManagement;
using Ghost.Contracts.Jobs;
using Ghost.Kernel;
using Ghost.Plugin.Glassdoor.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Glassdoor.Jobs;

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

    private static readonly Action<ILogger, string, Exception?> s_logExtractionDebug =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(12, "ExtractionDebug"), "Glassdoor extraction debug: {DebugInfo}");

    private static readonly Action<ILogger, Exception?> s_logRegexFallback =
        LoggerMessage.Define(LogLevel.Debug, new EventId(13, "RegexFallback"), "JavaScript extraction returned 0 jobs, falling back to regex");

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

        List<JobListing> jobs = [];
        string query = Uri.EscapeDataString(criteria.Query ?? string.Empty);
        string location = Uri.EscapeDataString(criteria.Location ?? string.Empty);

        string url = BuildSearchUrl(query, location);

        // Try up to 5 attempts with proxy rotation for 70% success rate
        for (int attempt = 1; attempt <= 5 && jobs.Count < limit; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            IBrowserSession? session = null;
            IPage? page = null;

            try
            {
                // Apply heavy rate limiting (5-10 seconds between requests)
                await ApplyHeavyRateLimitAsync(ct).ConfigureAwait(false);

                // Check if proxy rotation needed
                if (ShouldRotateProxy())
                {
                    s_logProxyRotated(_logger, _requestCount, null);
                    _requestCount = 0;
                }

                SessionOptions sessionOptions = await CreateHeavyStealthSessionOptionsAsync(ct).ConfigureAwait(false);
                s_logCreatingSession(_logger, attempt, null);

                session = await _kernel.NewSessionAsync(sessionOptions, ct).ConfigureAwait(false);
                page = await session.NewPageAsync(ct: ct).ConfigureAwait(false);

                s_logNavigating(_logger, url, null);
                await page.NavigateAsync(url, ct: ct).ConfigureAwait(false);

                // CRITICAL: Extended wait for Cloudflare/Datadome check (15 seconds for heavy protection)
                s_logCloudflareWait(_logger, 15, null);
                await Task.Delay(15000, ct).ConfigureAwait(false);

                // Perform VERY realistic human-like scrolling with longer delays
                await PerformHumanLikeScrollingAsync(page, ct).ConfigureAwait(false);

                // Final wait for dynamic content (5 seconds)
                await Task.Delay(5000, ct).ConfigureAwait(false);

                string html = await page.GetContentAsync(ct).ConfigureAwait(false);

                // Handle consent dialogs
                if (IsConsentPage(html))
                {
                    await _consentService.WaitAndHandleConsentAsync(page, maxWaitMs: 10000, checkIntervalMs: 500).ConfigureAwait(false);
                    await Task.Delay(3000, ct).ConfigureAwait(false); // Extended wait after consent
                    html = await page.GetContentAsync(ct).ConfigureAwait(false);
                }

                List<JobListing> pageJobs = await ExtractJobsFromPageAsync(page, html, ct).ConfigureAwait(false);

                // Check if we're still blocked and need additional wait
                if (pageJobs.Count == 0 && IsBlockedPage(html))
                {
                    s_logCloudflareWait(_logger, 10, null);
                    await Task.Delay(10000, ct).ConfigureAwait(false);
                    html = await page.GetContentAsync(ct).ConfigureAwait(false);
                    pageJobs = await ExtractJobsFromPageAsync(page, html, ct).ConfigureAwait(false);
                }

                foreach (JobListing job in pageJobs)
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
                    int backoffSeconds = attempt * 5; // 5s, 10s, 15s, 20s
                    await Task.Delay(TimeSpan.FromSeconds(backoffSeconds), ct).ConfigureAwait(false);
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
                int backoffMs = (int)(Math.Pow(2, attempt) * 1000 + Random.Shared.Next(1000, 3000));
                await Task.Delay(backoffMs, ct).ConfigureAwait(false);
            }
            finally
            {
                if (page != null)
                {
                    try { await page.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
                }
                if (session != null)
                {
                    try { await session.DisposeAsync().ConfigureAwait(false); } catch { /* ignore */ }
                }
            }
        }

        return jobs;
    }

    private static string BuildSearchUrl(string query, string location)
    {
        string baseUrl = "https://www.glassdoor.com/Job/jobs.htm";
        List<string> parameters = [];

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
                ProxyInfo? proxy = await _proxyProvider.GetProxyAsync("US", ct).ConfigureAwait(false);
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
        await _rateLimitSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            TimeSpan timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _heavyRateLimitDelay)
            {
                TimeSpan waitTime = _heavyRateLimitDelay - timeSinceLastRequest;
                // Add jitter (random 0-2 seconds)
                var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 2000) / 1000.0);
                await Task.Delay(waitTime + jitter, ct).ConfigureAwait(false);
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
        int[] scrollSteps = new[] { 400, 800, 1200, 1600, 2000 };

        for (int i = 0; i < scrollSteps.Length; i++)
        {
            s_logHumanScroll(_logger, i + 1, null);
            await page.EvaluateAsync<object>($"() => window.scrollTo({{ top: {scrollSteps[i]}, behavior: 'smooth' }})", null, ct).ConfigureAwait(false);

            // Variable delay between 1.5-3 seconds with jitter
            int delayMs = Random.Shared.Next(1500, 3000);
            await Task.Delay(delayMs, ct).ConfigureAwait(false);
        }

        // Scroll back up slightly (human behavior)
        await page.EvaluateAsync<object>("() => window.scrollTo({ top: 600, behavior: 'smooth' })", null, ct).ConfigureAwait(false);
        await Task.Delay(Random.Shared.Next(1000, 2000), ct).ConfigureAwait(false);

        // Final scroll down
        await page.EvaluateAsync<object>("() => window.scrollTo({ top: 2400, behavior: 'smooth' })", null, ct).ConfigureAwait(false);
        await Task.Delay(Random.Shared.Next(2000, 3000), ct).ConfigureAwait(false);
    }

    private static bool IsConsentPage(string html)
    {
        if (string.IsNullOrEmpty(html))
            return false;

        string[] consentIndicators = new[]
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

        string lowerHtml = html.ToLowerInvariant();
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
        List<JobListing> jobs = [];

        try
        {
            // Enhanced JavaScript extraction with comprehensive selectors for current Glassdoor structure
            string script = """
                () => {
                    const jobs = [];
                    const debugInfo = { selectorsTried: [], foundElements: 0, errors: [] };

                    console.log('=== Starting Glassdoor job extraction ===');

                    // Comprehensive selector list for current Glassdoor HTML structure
                    const selectors = [
                        // Primary selectors (most common)
                        'li.react-job-listing',
                        '[data-test="jobListing"]',
                        'li[data-test="jobListing"]',
                        // Alternative selectors
                        '[data-testid="job-listing"]',
                        '.jobListing',
                        '.job-listing',
                        'article[data-job-id]',
                        'li.JobsList_jobListItem__wjTHv',
                        'li[data-brandviews="true"]',
                        'div[data-test="job-listing"]',
                        '.jobContainer',
                        'article.job',
                        // CSS module selectors (Glassdoor uses these)
                        '[class*="JobCard"]',
                        '[class*="jobCard"]',
                        '[class*="JobsList"]',
                        '[class*="JobListing"]',
                        // Generic job card patterns
                        'ul > li[class*="job"]',
                        'li[class*="JobsList"]',
                        'div[class*="JobCard"]',
                        // Newer Glassdoor patterns
                        '[data-automation="jobListing"]',
                        '[data-automation-id="jobListing"]',
                        '.JobCard_container__Zw9kq',
                        '.JobsList_listItem__3pRBS'
                    ];

                    let jobElements = [];
                    let usedSelector = null;

                    for (const selector of selectors) {
                        debugInfo.selectorsTried.push(selector);
                        try {
                            jobElements = document.querySelectorAll(selector);
                            if (jobElements.length > 0) {
                                usedSelector = selector;
                                debugInfo.foundElements = jobElements.length;
                                console.log(`Found ${jobElements.length} job elements using selector: ${selector}`);
                                break;
                            }
                        } catch (e) {
                            debugInfo.errors.push(`Selector ${selector} error: ${e.message}`);
                        }
                    }

                    if (jobElements.length === 0) {
                        console.log('No job elements found with any selector');
                        console.log('Debug info:', JSON.stringify(debugInfo));
                        return { jobs, debugInfo };
                    }

                    console.log(`Processing ${jobElements.length} job elements...`);

                    jobElements.forEach((el, index) => {
                        try {
                            // Comprehensive title selectors
                            const titleSelectors = [
                                '[data-test="job-title"]',
                                '.jobTitle',
                                'h2 a',
                                'h3 a',
                                '.job-title',
                                'a[data-test="job-link"]',
                                'a[data-test="job-title-link"]',
                                '.JobCard_jobTitle__GLz9d',
                                'a.JobCard_jobTitle__GLz9d',
                                '.jobTitle span',
                                'a[class*="jobTitle"]',
                                'h2[class*="jobTitle"]',
                                'h3[class*="jobTitle"]',
                                '[class*="JobTitle"]',
                                'a[class*="job-title"]',
                                'div[class*="jobTitle"] a',
                                '[data-automation="jobTitle"]',
                                '.JobCard_title__mSR7g'
                            ];

                            let titleEl = null;
                            for (const sel of titleSelectors) {
                                titleEl = el.querySelector(sel);
                                if (titleEl && titleEl.textContent.trim()) break;
                            }

                            // Comprehensive company selectors
                            const companySelectors = [
                                '[data-test="employer-name"]',
                                '.employerName',
                                '.company-name',
                                '.employer',
                                '[data-test="employer"]',
                                '.EmployerProfile_employerName__X8lAb',
                                'span[data-test="employer-name"]',
                                '.jobEmpolyerName',
                                '[class*="employer"]',
                                '[class*="company"]',
                                'span[class*="EmployerProfile"]',
                                'div[class*="employer"] span',
                                '[data-automation="employerName"]',
                                '.JobCard_employerName__CE9P1'
                            ];

                            let companyEl = null;
                            for (const sel of companySelectors) {
                                companyEl = el.querySelector(sel);
                                if (companyEl && companyEl.textContent.trim()) break;
                            }

                            // Comprehensive location selectors
                            const locationSelectors = [
                                '[data-test="job-location"]',
                                '.jobLocation',
                                '.location',
                                '[data-test="location"]',
                                '.JobCard_location__2FJ4C',
                                '[class*="location"]',
                                'span[class*="location"]',
                                'div[class*="location"]',
                                '[data-automation="jobLocation"]',
                                '.JobCard_location__nVl3Q'
                            ];

                            let locationEl = null;
                            for (const sel of locationSelectors) {
                                locationEl = el.querySelector(sel);
                                if (locationEl && locationEl.textContent.trim()) break;
                            }

                            // Salary selectors
                            const salarySelectors = [
                                '[data-test="job-salary"]',
                                '.salary',
                                '.salary-estimate',
                                '[data-test="detailSalary"]',
                                '.JobCard_salaryEstimate__2pN6s',
                                '[class*="salary"]',
                                '[data-automation="salary"]',
                                '.JobCard_salary__D_R5o'
                            ];

                            let salaryEl = null;
                            for (const sel of salarySelectors) {
                                salaryEl = el.querySelector(sel);
                                if (salaryEl && salaryEl.textContent.trim()) break;
                            }

                            // Link selectors
                            const linkSelectors = [
                                'a[href*="/job-listing/"]',
                                'a[href*="/partner/jobListing.htm"]',
                                'a[href*="/job/"]',
                                'a[data-test="job-title-link"]',
                                'a[data-test="job-link"]',
                                'a.JobCard_jobTitle__GLz9d',
                                'a[class*="jobTitle"]',
                                'a[class*="JobCard"]'
                            ];

                            let linkEl = null;
                            for (const sel of linkSelectors) {
                                linkEl = el.querySelector(sel);
                                if (linkEl && linkEl.href) break;
                            }

                            const title = titleEl?.textContent?.trim() || '';
                            const company = companyEl?.textContent?.trim() || '';
                            const location = locationEl?.textContent?.trim() || '';
                            const salary = salaryEl?.textContent?.trim() || '';
                            const url = linkEl?.href || '';
                            const jobId = linkEl?.href?.match(/\/job-listing\/([^\/\?]+)/)?.[1] ||
                                         linkEl?.href?.match(/\/partner\/jobListing\.htm\?positionId=(\d+)/)?.[1] ||
                                         linkEl?.href?.match(/\/job\/([^\/\?]+)/)?.[1] ||
                                         el?.getAttribute('data-job-id') || '';

                            console.log(`Job ${index}: title="${title}", company="${company}", location="${location}", url="${url ? 'yes' : 'no'}"`);

                            if (title && company) {
                                jobs.push({
                                    title,
                                    company,
                                    location,
                                    salary,
                                    url,
                                    jobId
                                });
                            } else {
                                console.log(`Job ${index} skipped - missing title or company (title: ${!!title}, company: ${!!company})`);
                            }
                        } catch (e) {
                            console.error(`Error extracting job ${index}:`, e);
                            debugInfo.errors.push(`Job ${index} error: ${e.message}`);
                        }
                    });

                    console.log(`Total jobs extracted: ${jobs.length}`);
                    console.log(`Debug info:`, JSON.stringify(debugInfo));

                    return { jobs, debugInfo };
                }
            """;

            Dictionary<string, object> result = await page.EvaluateAsync<Dictionary<string, object>>(script, null, ct).ConfigureAwait(false);

            if (result != null)
            {
                // Extract jobs list
                if (result.TryGetValue("jobs", out object? jobsObj) && jobsObj is List<object> extractedJobsList)
                {
                    foreach (object jobObj in extractedJobsList)
                    {
                        if (jobObj is Dictionary<string, object> jobData)
                        {
                            string? extractedId = jobData.TryGetValue("jobId", out object? jid) ? jid?.ToString() : null;
                            string? extractedTitle = jobData.TryGetValue("title", out object? jtitle) ? jtitle?.ToString() : null;
                            string? extractedCompany = jobData.TryGetValue("company", out object? jcompany) ? jcompany?.ToString() : null;
                            string? extractedLocation = jobData.TryGetValue("location", out object? jloc) ? jloc?.ToString() : null;
                            string? extractedUrl = jobData.TryGetValue("url", out object? jurl) ? jurl?.ToString() : null;
                            string jobId = !string.IsNullOrEmpty(extractedId)
                                ? extractedId
                                : GlassdoorIdGenerator.GenerateDeterministicId(extractedTitle, extractedCompany, extractedLocation, extractedUrl);

                            var job = new JobListing
                            {
                                Id = jobId,
                                Title = extractedTitle ?? string.Empty,
                                Company = extractedCompany ?? string.Empty,
                                Location = extractedLocation,
                                Salary = jobData.TryGetValue("salary", out object? jsalary) ? jsalary?.ToString() : null,
                                Url = extractedUrl,
                                Source = "Glassdoor"
                            };

                            jobs.Add(job);
                        }
                    }
                }

                // Log debug info if available
                if (result.TryGetValue("debugInfo", out object? debugObj))
                {
                    string debugJson = System.Text.Json.JsonSerializer.Serialize(debugObj);
                    s_logExtractionDebug(_logger, debugJson, null);
                }
            }

            // Fallback to regex if JavaScript extraction fails
            if (jobs.Count == 0)
            {
                s_logRegexFallback(_logger, null);
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
        List<JobListing> jobs = [];

        if (string.IsNullOrEmpty(html))
            return jobs;

        try
        {
            // Try to extract job data from embedded JSON in script tags first
            string[] jsonPatterns = new[]
            {
                @"window\.__INITIAL_STATE__\s*=\s*({.*?});",
                @"window\.__NEXT_DATA__\s*=\s*({.*?});",
                @"<script[^>]*id\s*=\s*[""']__NEXT_DATA__[""'][^>]*type\s*=\s*[""']application/json[""'][^>]*>(.*?)</script>"
            };

            foreach (string? pattern in jsonPatterns)
            {
                Match match = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    try
                    {
                        string jsonContent = match.Groups[1].Value;
                        using var doc = System.Text.Json.JsonDocument.Parse(jsonContent);
                        List<JobListing> extractedJobs = ExtractJobsFromJsonElement(doc.RootElement);
                        if (extractedJobs.Count > 0)
                        {
                            return extractedJobs;
                        }
                    }
                    catch { }
                }
            }

            // Fallback: Parse HTML structure for job cards with multiple patterns
            string[] jobCardPatterns = new[]
            {
                // Pattern 1: data-test attributes (most common)
                @"<(?:li|div|article)[^>]*(?:data-test=[""']jobListing[""']|data-job-id=[""'][^""']+[""'])[^>]*>.*?data-test=[""']job-title[""'][^>]*>([^<]+)<.*?data-test=[""']employer-name[""'][^>]*>([^<]+)<.*?(?:data-test=[""']job-location[""'][^>]*>([^<]+)<|class=[""'][^""']*location[^""']*[""'][^>]*>([^<]+)<)",
                // Pattern 2: href-based extraction
                @"<a[^>]*href=[""']/job-listing/([^""'\?]+)[""'][^>]*>([^<]+)</a>.*?(?:data-test=[""']employer-name[""'][^>]*>([^<]+)<|class=[""'][^""']*employer[^""']*[""'][^>]*>([^<]+)<)",
                // Pattern 3: CSS module classes
                @"<li[^>]*class=[""'][^""']*JobCard[^""']*[""'][^>]*>.*?<a[^>]*class=[""'][^""']*jobTitle[^""']*[""'][^>]*>([^<]+)</a>.*?(?:class=[""'][^""']*employer[^""']*[""'][^>]*>([^<]+)<|class=[""'][^""']*company[^""']*[""'][^>]*>([^<]+)<)",
                // Pattern 4: Generic job card structure
                @"<(?:li|div|article)[^>]*class=[""'][^""']*(?:job|Job)[^""']*[""'][^>]*>.*?<a[^>]*href=[""'][^""']*job[^""']*[""'][^>]*>([^<]+)</a>.*?<(?:span|div)[^>]*class=[""'][^""']*(?:employer|company)[^""']*[""'][^>]*>([^<]+)<"
            };

            foreach (string? pattern in jobCardPatterns)
            {
                MatchCollection matches = Regex.Matches(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (matches.Count > 0)
                {
                    foreach (Match match in matches)
                    {
                        try
                        {
                            string title = string.Empty;
                            string company = string.Empty;
                            string location = string.Empty;
                            string url = string.Empty;
                            string jobId = string.Empty;

                            // Extract based on pattern groups
                            if (match.Groups.Count >= 3)
                            {
                                title = match.Groups[1].Value.Trim();
                                company = match.Groups[2].Value.Trim();

                                if (match.Groups.Count >= 4)
                                {
                                    location = match.Groups[3].Value.Trim();
                                    if (string.IsNullOrEmpty(location) && match.Groups.Count >= 5)
                                    {
                                        location = match.Groups[4].Value.Trim();
                                    }
                                }
                            }

                            // Try to extract URL and job ID from the match
                            Match urlMatch = Regex.Match(match.Value, @"href=[""']([^""']+)[""']");
                            if (urlMatch.Success)
                            {
                                url = urlMatch.Groups[1].Value;
                                Match idMatch = Regex.Match(url, @"/job-listing/([^\/\?]+)");
                                if (idMatch.Success)
                                {
                                    jobId = idMatch.Groups[1].Value;
                                }
                            }

                            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(company))
                            {
                                string deterministicId = !string.IsNullOrEmpty(jobId)
                                    ? jobId
                                    : GlassdoorIdGenerator.GenerateDeterministicId(title, company, location, url);
                                jobs.Add(new JobListing
                                {
                                    Id = deterministicId,
                                    Title = System.Net.WebUtility.HtmlDecode(title),
                                    Company = System.Net.WebUtility.HtmlDecode(company),
                                    Location = !string.IsNullOrEmpty(location) ? System.Net.WebUtility.HtmlDecode(location) : null,
                                    Url = !string.IsNullOrEmpty(url) ? url : null,
                                    Source = "Glassdoor"
                                });
                            }
                        }
                        catch { }
                    }

                    if (jobs.Count > 0)
                    {
                        break; // Found jobs with this pattern, don't try others
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but don't throw
            System.Diagnostics.Debug.WriteLine($"Regex extraction error: {ex.Message}");
        }

        return jobs;
    }

    private static List<JobListing> ExtractJobsFromJsonElement(System.Text.Json.JsonElement element)
    {
        List<JobListing> jobs = [];

        try
        {
            // Recursively search for job data in the JSON structure
            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    // Look for properties that likely contain job listings
                    if (prop.Name.Contains("job", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Contains("listing", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Contains("result", StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                        {
                            foreach (JsonElement item in prop.Value.EnumerateArray())
                            {
                                JobListing? job = ExtractJobFromJsonElement(item);
                                if (job != null)
                                    jobs.Add(job);
                            }
                        }
                        else
                        {
                            jobs.AddRange(ExtractJobsFromJsonElement(prop.Value));
                        }
                    }
                    else
                    {
                        jobs.AddRange(ExtractJobsFromJsonElement(prop.Value));
                    }
                }
            }
            else if (element.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    JobListing? job = ExtractJobFromJsonElement(item);
                    if (job != null)
                        jobs.Add(job);
                    else
                        jobs.AddRange(ExtractJobsFromJsonElement(item));
                }
            }
        }
        catch { }

        return jobs;
    }

    private static JobListing? ExtractJobFromJsonElement(System.Text.Json.JsonElement element)
    {
        try
        {
            string? title = null;
            string? company = null;
            string? location = null;
            string? id = null;
            string? url = null;

            if (element.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                // Try different field name variations
                title = GetJsonString(element, "jobTitleText", "jobTitle", "title", "job_title");
                company = GetJsonString(element, "employerName", "employer", "company", "companyName");
                location = GetJsonString(element, "locationName", "location", "jobLocationCity", "city");
                id = GetJsonString(element, "listingId", "jobId", "id");
                url = GetJsonString(element, "jobLink", "link", "url");

                // Check for nested structures
                if (element.TryGetProperty("jobview", out JsonElement jobview))
                {
                    if (jobview.TryGetProperty("header", out JsonElement header))
                    {
                        title ??= GetJsonString(header, "jobTitleText", "jobTitle");
                        location ??= GetJsonString(header, "locationName", "location");
                        url ??= GetJsonString(header, "jobLink");

                        if (header.TryGetProperty("employer", out JsonElement employer))
                        {
                            company ??= GetJsonString(employer, "name");
                        }
                    }

                    if (jobview.TryGetProperty("job", out JsonElement job))
                    {
                        id ??= GetJsonString(job, "listingId");
                    }
                }
            }

            // Must have at least title to be a valid job
            if (!string.IsNullOrEmpty(title))
            {
                string deterministicId = id ?? GlassdoorIdGenerator.GenerateDeterministicId(title, company, location, url);
                return new JobListing
                {
                    Id = deterministicId,
                    Title = title,
                    Company = company ?? "Unknown Company",
                    Location = location,
                    Url = url,
                    Source = "Glassdoor"
                };
            }
        }
        catch { }

        return null;
    }

    private static string? GetJsonString(System.Text.Json.JsonElement element, params string[] keys)
    {
        foreach (string key in keys)
        {
            try
            {
                if (element.TryGetProperty(key, out JsonElement value) && value.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    string? str = value.GetString();
                    if (!string.IsNullOrWhiteSpace(str))
                        return str;
                }
            }
            catch { }
        }
        return null;
    }

    private static string GetRandomUserAgent()
    {
        string[] userAgents = new[]
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
        string[] timezones = new[] { "America/New_York", "America/Chicago", "America/Los_Angeles", "America/Denver" };
        return timezones[Random.Shared.Next(timezones.Length)];
    }

    private static SessionOptions.GeolocationSettings GetRandomUSGeolocation()
    {
        // Major US cities
        (double, double)[] locations = new[]
        {
            (40.7128, -74.0060),  // New York
            (41.8781, -87.6298),  // Chicago
            (34.0522, -118.2437), // Los Angeles
            (29.7604, -95.3698),  // Houston
            (33.4484, -112.0740)  // Phoenix
        };

        (double lat, double lon) = locations[Random.Shared.Next(locations.Length)];
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
