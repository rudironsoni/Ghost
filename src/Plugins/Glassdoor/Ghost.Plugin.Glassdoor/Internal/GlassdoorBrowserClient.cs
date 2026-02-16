using System.Text.Json;
using System.Text.RegularExpressions;
using Ghost.ConsentManagement;
using Ghost.Contracts.Jobs;
using Ghost.Kernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.Glassdoor.Internal;

public sealed class GlassdoorBrowserClient : IDisposable
{
    private readonly IGhostKernel _kernel;
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

    private static readonly Action<ILogger, string?, string?, int, Exception?> s_logSearchStarting =
        LoggerMessage.Define<string?, string?, int>(LogLevel.Information, new EventId(13, "SearchStarting"), "Starting browser search for query='{Query}', location='{Location}', limit={Limit}");

    private static readonly Action<ILogger, string, Exception?> s_logBuiltUrl =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(14, "BuiltUrl"), "Built URL={Url}");

    private static readonly Action<ILogger, int, Exception?> s_logCreatingSession =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(15, "CreatingSession"), "Attempt {Attempt}, creating browser session");

    private static readonly Action<ILogger, Exception?> s_logNavigatingToUrl =
        LoggerMessage.Define(LogLevel.Debug, new EventId(16, "NavigatingToUrl"), "Navigating to URL");

    private static readonly Action<ILogger, int, Exception?> s_logGotHtmlContent =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(17, "GotHtmlContent"), "Got HTML content, length={Length}");

    private static readonly Action<ILogger, Exception?> s_logHandlingConsent =
        LoggerMessage.Define(LogLevel.Debug, new EventId(18, "HandlingConsent"), "Handling consent page");

    private static readonly Action<ILogger, int, Exception?> s_logAfterConsentHtml =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(19, "AfterConsentHtml"), "After consent handling, HTML length={Length}");

    private static readonly Action<ILogger, int, Exception?> s_logExtractedJobs =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(20, "ExtractedJobs"), "Extracted {JobCount} jobs from page");

    private static readonly Action<ILogger, Exception?> s_logMaxAttemptsReached =
        LoggerMessage.Define(LogLevel.Warning, new EventId(21, "MaxAttemptsReached"), "Max attempts reached, giving up");

    private static readonly Action<ILogger, string, Exception?> s_logExtractionDebug =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(22, "ExtractionDebug"), "Glassdoor extraction debug: {DebugInfo}");

    private static readonly Action<ILogger, Exception?> s_logRegexFallback =
        LoggerMessage.Define(LogLevel.Debug, new EventId(23, "RegexFallback"), "JavaScript extraction returned 0 jobs, falling back to regex");

    private readonly ConsentManagerService _consentService;

    public GlassdoorBrowserClient(
        IGhostKernel kernel,
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
        s_logSearchStarting(_logger, criteria.Query, criteria.Location, limit, null);

        List<JobListing> jobs = [];
        string query = Uri.EscapeDataString(criteria.Query ?? string.Empty);
        string location = Uri.EscapeDataString(criteria.Location ?? string.Empty);

        string url = BuildSearchUrl(query, location);
        s_logBuiltUrl(_logger, url, null);

        for (int attempt = 1; attempt <= 3 && jobs.Count < limit; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            IBrowserSession? session = null;
            IPage? page = null;

            try
            {
                await ApplyRateLimitAsync(ct).ConfigureAwait(false);

                SessionOptions sessionOptions = await CreateSessionOptionsAsync(ct).ConfigureAwait(false);
                s_logSessionCreating(_logger, sessionOptions.Proxy?.Server ?? "None", null);
                s_logCreatingSession(_logger, attempt, null);

                session = await _kernel.NewSessionAsync(sessionOptions, ct).ConfigureAwait(false);
                page = await session.NewPageAsync(ct: ct).ConfigureAwait(false);

                s_logNavigating(_logger, url, null);
                s_logNavigatingToUrl(_logger, null);
                await page.NavigateAsync(url, ct: ct).ConfigureAwait(false);

                // CRITICAL: Wait for Cloudflare check to complete (10 seconds minimum)
                await Task.Delay(10000, ct).ConfigureAwait(false);

                // Perform realistic human-like scrolling to trigger lazy loading and avoid bot detection

                // Smooth scroll down in multiple steps
                await page.EvaluateAsync<object>("() => window.scrollTo({ top: 400, behavior: 'smooth' })", null, ct).ConfigureAwait(false);
                await Task.Delay(1200, ct).ConfigureAwait(false);

                await page.EvaluateAsync<object>("() => window.scrollTo({ top: 800, behavior: 'smooth' })", null, ct).ConfigureAwait(false);
                await Task.Delay(1500, ct).ConfigureAwait(false);

                await page.EvaluateAsync<object>("() => window.scrollTo({ top: 1200, behavior: 'smooth' })", null, ct).ConfigureAwait(false);
                await Task.Delay(1000, ct).ConfigureAwait(false);

                // Scroll back up slightly (human behavior)
                await page.EvaluateAsync<object>("() => window.scrollTo({ top: 600, behavior: 'smooth' })", null, ct).ConfigureAwait(false);
                await Task.Delay(800, ct).ConfigureAwait(false);

                // One more scroll down to load more jobs
                await page.EvaluateAsync<object>("() => window.scrollTo({ top: 1600, behavior: 'smooth' })", null, ct).ConfigureAwait(false);
                await Task.Delay(1500, ct).ConfigureAwait(false);

                // Final wait for any additional dynamic content
                await Task.Delay(3000, ct).ConfigureAwait(false);

                string html = await page.GetContentAsync(ct).ConfigureAwait(false);
                s_logGotHtmlContent(_logger, html.Length, null);

                if (IsConsentPage(html))
                {
                    s_logConsentDetected(_logger, null);
                    s_logHandlingConsent(_logger, null);
                    await _consentService.WaitAndHandleConsentAsync(page, maxWaitMs: 8000, checkIntervalMs: 500).ConfigureAwait(false);
                    await Task.Delay(2000, ct).ConfigureAwait(false); // Wait after consent
                    html = await page.GetContentAsync(ct).ConfigureAwait(false);
                    s_logAfterConsentHtml(_logger, html.Length, null);
                }

                List<JobListing> pageJobs = await ExtractJobsFromPageAsync(page, html, ct).ConfigureAwait(false);
                s_logExtractedJobs(_logger, pageJobs.Count, null);

                // Check if we're blocked and need additional wait
                if (pageJobs.Count == 0)
                {
                    if (html.Contains("cloudflare", StringComparison.OrdinalIgnoreCase) ||
                        html.Contains("just a moment", StringComparison.OrdinalIgnoreCase) ||
                        html.Contains("checking your browser", StringComparison.OrdinalIgnoreCase))
                    {
                        await Task.Delay(5000, ct).ConfigureAwait(false);
                        html = await page.GetContentAsync(ct).ConfigureAwait(false);
                        pageJobs = await ExtractJobsFromPageAsync(page, html, ct).ConfigureAwait(false);
                        s_logExtractedJobs(_logger, pageJobs.Count, null);
                    }
                }

                foreach (JobListing job in pageJobs)
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
                    List<JobListing> moreJobs = await TryLoadMoreResultsAsync(page, limit - jobs.Count, ct).ConfigureAwait(false);
                    foreach (JobListing job in moreJobs)
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
                    s_logMaxAttemptsReached(_logger, null);
                    break;
                }
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct).ConfigureAwait(false);
            }
            finally
            {
                if (page != null)
                {
                    try { await page.DisposeAsync().ConfigureAwait(false); } catch { }
                }
                if (session != null)
                {
                    try { await session.DisposeAsync().ConfigureAwait(false); } catch { }
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
        var options = new SessionOptions
        {
            // Use realistic viewport size
            ViewportWidth = 1920,
            ViewportHeight = 1080,
            // Use realistic user agent matching Chrome 120
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            // Set locale and timezone for US
            Locale = "en-US",
            TimezoneId = "America/New_York",
            // Set New York geolocation for more realistic browsing
            Geolocation = new SessionOptions.GeolocationSettings(40.7128, -74.0060, 100),
            // Explicitly disable proxy for Glassdoor to avoid SOCKS5 connection issues
            Proxy = null
        };

        // Note: Proxy is intentionally disabled for Glassdoor browser scraping
        // The proxyProvider is set to null in GlassdoorExtension.cs registration
        // to prevent SOCKS5 authentication failures that Glassdoor doesn't support
        if (_options.Value.ProxyEnabled && _proxyProvider != null)
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

    private async Task ApplyRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            TimeSpan timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _rateLimitDelay)
            {
                TimeSpan waitTime = _rateLimitDelay - timeSinceLastRequest;
                await Task.Delay(waitTime, ct).ConfigureAwait(false);
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

        string[] consentIndicators = new[]
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

        string lowerHtml = html.ToLowerInvariant();
        return consentIndicators.Any(indicator => lowerHtml.Contains(indicator));
    }

    private async Task<List<JobListing>> ExtractJobsFromPageAsync(IPage page, string html, CancellationToken ct)
    {
        List<JobListing> jobs = [];

        try
        {
            // Enhanced JavaScript extraction with extensive selector fallbacks and detailed logging
            string script = """
                () => {
                    const jobs = [];

                    console.log('=== Starting job extraction ===');

                    // Try multiple selector strategies for Glassdoor's evolving DOM structure
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
                        '[class*="jobCard"]',
                        'ul > li[class*="job"]',
                        'li[class*="JobsList"]',
                        'div[class*="JobCard"]'
                    ];

                    let jobElements = [];
                    let usedSelector = null;

                    for (const selector of selectors) {
                        jobElements = document.querySelectorAll(selector);
                        if (jobElements.length > 0) {
                            usedSelector = selector;
                            console.log(`Found ${jobElements.length} job elements using selector: ${selector}`);
                            break;
                        }
                    }

                    if (jobElements.length === 0) {
                        console.log('No job elements found with any selector');
                        return { jobs: [], debug: { usedSelector, elementCount: 0, bodyLength: document.body.innerHTML.length } };
                    }

                    jobElements.forEach((el, index) => {
                        try {
                            // Try multiple title selector patterns
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
                                'div[class*="jobTitle"] a'
                            ];

                            let titleEl = null;
                            for (const sel of titleSelectors) {
                                titleEl = el.querySelector(sel);
                                if (titleEl && titleEl.textContent.trim()) break;
                            }

                            // Try multiple company selector patterns
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
                                'div[class*="employer"] span'
                            ];

                            let companyEl = null;
                            for (const sel of companySelectors) {
                                companyEl = el.querySelector(sel);
                                if (companyEl && companyEl.textContent.trim()) break;
                            }

                            // Try multiple location selector patterns
                            const locationSelectors = [
                                '[data-test="job-location"]',
                                '.jobLocation',
                                '.location',
                                '[data-test="location"]',
                                '.JobCard_location__2FJ4C',
                                '[class*="location"]',
                                'span[class*="location"]',
                                'div[class*="location"]'
                            ];

                            let locationEl = null;
                            for (const sel of locationSelectors) {
                                locationEl = el.querySelector(sel);
                                if (locationEl && locationEl.textContent.trim()) break;
                            }

                            // Try salary selectors
                            const salarySelectors = [
                                '[data-test="job-salary"]',
                                '.salary',
                                '.salary-estimate',
                                '[data-test="detailSalary"]',
                                '.JobCard_salaryEstimate__2pN6s',
                                '[class*="salary"]'
                            ];

                            let salaryEl = null;
                            for (const sel of salarySelectors) {
                                salaryEl = el.querySelector(sel);
                                if (salaryEl && salaryEl.textContent.trim()) break;
                            }

                            // Try link selectors
                            const linkSelectors = [
                                'a[href*="/job-listing/"]',
                                'a[href*="/job/"]',
                                'a[data-test="job-title-link"]',
                                'a[data-test="job-link"]',
                                'a.JobCard_jobTitle__GLz9d',
                                'a[class*="jobTitle"]'
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
                                         linkEl?.href?.match(/\/job\/([^\/\?]+)/)?.[1] || '';

                            console.log(`Job ${index}: title="${title}", company="${company}", location="${location}"`);

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
                                console.log(`Job ${index} skipped - missing title or company`);
                            }
                        } catch (e) {
                            console.error(`Error extracting job ${index}:`, e);
                        }
                    });

                    console.log(`Total jobs extracted: ${jobs.length}`);

                    return {
                        jobs,
                        debug: {
                            usedSelector,
                            elementCount: jobElements.length,
                            bodyLength: document.body.innerHTML.length
                        }
                    };
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
                            var job = new JobListing
                            {
                                Id = jobData.TryGetValue("jobId", out object? id) && !string.IsNullOrEmpty(id?.ToString())
                                    ? id.ToString()!
                                    : Guid.NewGuid().ToString(),
                                Title = jobData.TryGetValue("title", out object? title) ? title?.ToString() ?? string.Empty : string.Empty,
                                Company = jobData.TryGetValue("company", out object? company) ? company?.ToString() ?? string.Empty : string.Empty,
                                Location = jobData.TryGetValue("location", out object? loc) ? loc?.ToString() : null,
                                Salary = jobData.TryGetValue("salary", out object? salary) ? salary?.ToString() : null,
                                Url = jobData.TryGetValue("url", out object? url) ? url?.ToString() : null,
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
                                jobs.Add(new JobListing
                                {
                                    Id = !string.IsNullOrEmpty(jobId) ? jobId : Guid.NewGuid().ToString(),
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
                return new JobListing
                {
                    Id = id ?? Guid.NewGuid().ToString(),
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

    private async Task<List<JobListing>> TryLoadMoreResultsAsync(IPage page, int remaining, CancellationToken ct)
    {
        List<JobListing> jobs = [];

        try
        {
            string[] loadMoreSelectors = new[]
            {
                "button[data-test='load-more']",
                "button:has-text('Load More')",
                "button:has-text('Show More')",
                "a[aria-label*='Next']",
                "button[class*='next']",
                "button[class*='loadMore']"
            };

            foreach (string? selector in loadMoreSelectors)
            {
                try
                {
                    IElement? element = await page.QuerySelectorAsync(selector, ct).ConfigureAwait(false);
                    if (element != null)
                    {
                        await element.ClickAsync(ct: ct).ConfigureAwait(false);
                        await Task.Delay(2000, ct).ConfigureAwait(false);

                        string html = await page.GetContentAsync(ct).ConfigureAwait(false);
                        List<JobListing> newJobs = await ExtractJobsFromPageAsync(page, html, ct).ConfigureAwait(false);

                        foreach (JobListing job in newJobs)
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
                await page.EvaluateAsync<object>("() => window.scrollTo(0, document.body.scrollHeight)", null, ct).ConfigureAwait(false);
                await Task.Delay(2000, ct).ConfigureAwait(false);

                string html = await page.GetContentAsync(ct).ConfigureAwait(false);
                List<JobListing> newJobs = await ExtractJobsFromPageAsync(page, html, ct).ConfigureAwait(false);

                foreach (JobListing job in newJobs)
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
