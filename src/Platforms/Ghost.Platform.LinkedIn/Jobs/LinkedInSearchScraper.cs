using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.ConsentManagement;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Ghost.Platform.LinkedIn.Internal;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.LinkedIn.Jobs;

/// <summary>
/// Production-grade LinkedIn job search scraper with full stealth suite:
/// - Patchright browser (anti-detection)
/// - TLS fingerprint randomization
/// - Behavioral mimicry (Bezier mouse, human delays)
/// - Free proxy rotation via RotatingProxyPool
/// - Consent handler (28 CMPs)
/// - Session persistence via LinkedInSessionPool
/// Target: 99% reliability
/// </summary>
public sealed class LinkedInSearchScraper : IJobScraper, IDisposable
{
    private readonly LinkedInSessionPool _sessionPool;
    private readonly IProxyProvider? _proxyProvider;
    private readonly ConsentManagerService _consentService;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInSearchScraper> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(2); // 2-5s rate limit
    private bool _disposed;

    private static readonly Action<ILogger, string?, string?, int, Exception?> s_logSearchStarting =
        LoggerMessage.Define<string?, string?, int>(LogLevel.Information, new EventId(1, nameof(SearchJobsAsync)), "Starting LinkedIn search: query='{Query}', location='{Location}', limit={Limit}");

    private static readonly Action<ILogger, int, Exception?> s_logSearchCompleted =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(2, nameof(SearchJobsAsync)), "LinkedIn search completed: found {Count} jobs");

    private static readonly Action<ILogger, Exception?> s_logConsentHandled =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3, nameof(SearchJobsAsync)), "Consent dialog detected and handled");

    private static readonly Action<ILogger, int, Exception?> s_logJobsExtracted =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(4, nameof(SearchJobsAsync)), "Extracted {Count} jobs from page");

    private static readonly Action<ILogger, Exception?> s_logSearchFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(5, nameof(SearchJobsAsync)), "LinkedIn search failed");

    private static readonly Action<ILogger, Exception?> s_logJobNodeParseFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(6, nameof(ExtractJobsFromPageAsync)), "Failed to parse job node");

    public LinkedInSearchScraper(
        LinkedInSessionPool sessionPool,
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInSearchScraper> logger,
        IProxyProvider? proxyProvider = null)
    {
        _sessionPool = sessionPool ?? throw new ArgumentNullException(nameof(sessionPool));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInSearchScraper>.Instance;
        _proxyProvider = proxyProvider;
        _consentService = new ConsentManagerService(null);
    }

    public string PlatformName => "LinkedIn";

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var query = criteria.Query ?? string.Empty;
        var location = criteria.Location ?? string.Empty;
        var limit = criteria.MaxResults > 0 ? criteria.MaxResults : 25;

        s_logSearchStarting(_logger, query, location, limit, null);

        var jobs = new List<JobListing>();
        IBrowserSession? session = null;
        IPage? page = null;

        try
        {
            // Apply rate limiting (2-5s between requests with jitter)
            await ApplyRateLimitAsync(ct);

            // Acquire session from pool (with proxy rotation, TLS randomization, session persistence)
            session = await _sessionPool.AcquireAsync(ct);

            // Create new page with stealth options
            var pageOpts = _options.GetPageOptions();
            page = await session.NewPageAsync(pageOpts, ct: ct);

            // Navigate to search URL
            var url = BuildSearchUrl(query, location);
            var navOptions = new NavigationOptions { Timeout = 30_000, WaitUntil = WaitUntil.Load };
            await page.NavigateAsync(url, navOptions, ct: ct);
            await page.WaitForLoadStateAsync(ct: ct);

            // Handle consent dialogs (28 CMPs supported)
            var html = await page.GetContentAsync(ct);
            if (IsConsentPage(html))
            {
                await _consentService.WaitAndHandleConsentAsync(page, maxWaitMs: 10000, checkIntervalMs: 500);
                s_logConsentHandled(_logger, null);
                await Task.Delay(2000, ct); // Wait after consent
                html = await page.GetContentAsync(ct);
            }

            // Perform human-like scrolling (behavioral mimicry)
            await PerformHumanScrollingAsync(page, ct);

            // Extract jobs from page
            var pageJobs = await ExtractJobsFromPageAsync(page, limit, ct);
            s_logJobsExtracted(_logger, pageJobs.Count, null);

            jobs.AddRange(pageJobs);

            s_logSearchCompleted(_logger, jobs.Count, null);
            return jobs;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            s_logSearchFailed(_logger, ex);
            throw;
        }
        finally
        {
            if (page != null)
            {
                try { await page.DisposeAsync(); } catch { /* ignore */ }
            }
            if (session != null)
            {
                _sessionPool.Release(session);
            }
        }
    }

    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        // Delegate to LinkedInJobDetailsScraper
        throw new NotImplementedException("Use LinkedInJobDetailsScraper for job details");
    }

    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default)
    {
        throw new NotImplementedException("Apply not implemented in search scraper");
    }

    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default)
    {
        throw new NotImplementedException("GetApplications not implemented in search scraper");
    }

    public Task SaveJobAsync(string jobId, CancellationToken ct = default)
    {
        throw new NotImplementedException("SaveJob not implemented in search scraper");
    }

    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default)
    {
        throw new NotImplementedException("GetSavedJobs not implemented in search scraper");
    }

    private string BuildSearchUrl(string keywords, string location)
    {
        var q = Uri.EscapeDataString(keywords);
        var loc = Uri.EscapeDataString(location);
        return $"{_options.BaseUrl}/jobs/search?keywords={q}&location={loc}";
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
                // Add jitter (0-3 seconds)
                var jitter = TimeSpan.FromSeconds(Random.Shared.Next(0, 3000) / 1000.0);
                await Task.Delay(waitTime + jitter, ct);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    private async Task PerformHumanScrollingAsync(IPage page, CancellationToken ct)
    {
        // Scroll down in realistic steps with variable delays
        var scrollSteps = new[] { 300, 600, 900, 1200, 1500 };

        foreach (var scrollY in scrollSteps)
        {
            await page.EvaluateAsync<object>($"() => window.scrollTo({{ top: {scrollY}, behavior: 'smooth' }})", null, ct);

            // Variable delay between 800-2000ms
            var delayMs = Random.Shared.Next(800, 2000);
            await Task.Delay(delayMs, ct);
        }

        // Scroll back up slightly (human behavior)
        await page.EvaluateAsync<object>("() => window.scrollTo({ top: 400, behavior: 'smooth' })", null, ct);
        await Task.Delay(Random.Shared.Next(800, 1500), ct);

        // Final scroll down
        await page.EvaluateAsync<object>("() => window.scrollTo({ top: 1800, behavior: 'smooth' })", null, ct);
        await Task.Delay(Random.Shared.Next(1000, 2000), ct);
    }

    private async Task<List<JobListing>> ExtractJobsFromPageAsync(IPage page, int limit, CancellationToken ct)
    {
        var jobs = new List<JobListing>();

        // Query job listing elements
        var nodes = await page.QuerySelectorAllAsync(".jobs-search-results__list-item, .jobs-search__results-list li, .base-card", ct: ct);

        var count = 0;
        foreach (var node in nodes)
        {
            if (count++ >= limit) break;

            try
            {
                // Extract job ID
                string id = Guid.NewGuid().ToString();
                var idEl = await node.QuerySelectorAsync("[data-id], [data-entity-urn]", ct);
                if (idEl != null)
                {
                    var dataId = await idEl.GetAttributeAsync("data-id", ct);
                    var urn = await idEl.GetAttributeAsync("data-entity-urn", ct);
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

                // Extract title
                var titleEl = await node.QuerySelectorAsync(".job-card-list__title, .base-search-card__title", ct);
                string title = titleEl is not null ? (await titleEl.GetTextContentAsync(ct))?.Trim() ?? string.Empty : string.Empty;

                // Extract company
                var companyEl = await node.QuerySelectorAsync(".job-card-container__company-name, .base-search-card__subtitle", ct);
                string company = companyEl is not null ? (await companyEl.GetTextContentAsync(ct))?.Trim() ?? string.Empty : string.Empty;

                // Extract location
                var locationEl = await node.QuerySelectorAsync(".job-card-container__metadata-item, .job-search-card__location", ct);
                string locationText = locationEl is not null ? (await locationEl.GetTextContentAsync(ct))?.Trim() ?? string.Empty : string.Empty;

                // Extract URL
                string? jobUrl = null;
                var linkEl = await node.QuerySelectorAsync("a.base-card__full-link, a.job-card-list__title", ct);
                if (linkEl != null)
                {
                    jobUrl = await linkEl.GetAttributeAsync("href", ct);

                    // Try to extract job ID from URL if GUID
                    if (Guid.TryParse(id, out _) && !string.IsNullOrEmpty(jobUrl))
                    {
                        var urlIdMatch = System.Text.RegularExpressions.Regex.Match(jobUrl, @"-(\d{6,})(?:\?|$)");
                        if (urlIdMatch.Success && urlIdMatch.Groups[1].Success)
                        {
                            id = urlIdMatch.Groups[1].Value;
                        }
                    }
                }

                if (!string.IsNullOrEmpty(title))
                {
                    jobs.Add(new JobListing
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
                s_logJobNodeParseFailed(_logger, ex);
            }
        }

        return jobs;
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
            "privacy policy"
        };

        var lowerHtml = html.ToLowerInvariant();
        return consentIndicators.Any(indicator => lowerHtml.Contains(indicator));
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
