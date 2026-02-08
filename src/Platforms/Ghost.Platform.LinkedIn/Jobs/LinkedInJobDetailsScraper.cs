using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.ConsentManagement;
using Ghost.Contracts.Jobs;
using Ghost.Core;
using Ghost.Platform.LinkedIn.Entities;
using Ghost.Platform.LinkedIn.Internal;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Platform.LinkedIn.Jobs;

/// <summary>
/// Production-grade LinkedIn job details scraper with full stealth suite:
/// - Patchright browser (anti-detection)
/// - TLS fingerprint randomization
/// - Behavioral mimicry (Bezier mouse, human delays)
/// - Free proxy rotation via RotatingProxyPool
/// - Consent handler (28 CMPs)
/// - Session persistence via LinkedInSessionPool
/// Target: 99% reliability
/// </summary>
public sealed class LinkedInJobDetailsScraper : IDisposable
{
    private readonly LinkedInSessionPool _sessionPool;
    private readonly ConsentManagerService _consentService;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInJobDetailsScraper> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(2); // 2-5s rate limit
    private bool _disposed;

    private static readonly Action<ILogger, string, Exception?> s_logFetchStarting =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(GetJobDetailsAsync)), "Fetching LinkedIn job details: jobId={JobId}");

    private static readonly Action<ILogger, string, Exception?> s_logFetchCompleted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(2, nameof(GetJobDetailsAsync)), "LinkedIn job details fetched successfully: jobId={JobId}");

    private static readonly Action<ILogger, Exception?> s_logConsentHandled =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3, nameof(GetJobDetailsAsync)), "Consent dialog detected and handled");

    private static readonly Action<ILogger, string, string, Exception?> s_logDetailExtracted =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(4, nameof(GetJobDetailsAsync)), "Extracted job: title='{Title}', company='{Company}'");

    public LinkedInJobDetailsScraper(
        LinkedInSessionPool sessionPool,
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInJobDetailsScraper> logger)
    {
        _sessionPool = sessionPool ?? throw new ArgumentNullException(nameof(sessionPool));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInJobDetailsScraper>.Instance;
        _consentService = new ConsentManagerService(null);
    }

    /// <summary>
    /// Gets detailed job information for a specific job ID.
    /// </summary>
    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        s_logFetchStarting(_logger, jobId, null);

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

            // Navigate to job details URL
            var url = $"{_options.BaseUrl}/jobs/view/{jobId}";
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

            // Extract job details using EntityParser
            var context = new ExtractionContext
            {
                Content = html ?? string.Empty,
                SourceUrl = url,
                Timestamp = DateTime.UtcNow
            };

            var entity = EntityParser.ParseSingle<LinkedInJobEntity>(context);

            if (entity == null || !entity.Validate())
            {
                return new JobListing { Id = jobId, Url = url, Source = "LinkedIn" };
            }

            // Extract Job ID if not present
            var extractedJobId = entity.ExtractJobIdFromUrl() ?? jobId;

            // Check for Easy Apply
            bool isEasyApply = entity.IsEasyApply;

            // Parse JobType and ExperienceLevel from entity
            var jobType = entity.ParseJobType();
            var experienceLevel = entity.ParseExperienceLevel();

            // Parse PostedAt
            DateTimeOffset postedAt = entity.PostedAt ?? DateTimeOffset.UtcNow;

            var jobListing = new JobListing
            {
                Id = extractedJobId,
                Url = entity.Url ?? url,
                Title = entity.Title ?? string.Empty,
                Company = entity.Company ?? string.Empty,
                Location = entity.Location ?? string.Empty,
                Description = entity.Description ?? string.Empty,
                PostedAt = postedAt,
                IsEasyApply = isEasyApply,
                JobType = jobType,
                ExperienceLevel = experienceLevel,
                Salary = entity.Salary,
                Source = "LinkedIn"
            };

            s_logDetailExtracted(_logger, jobListing.Title, jobListing.Company, null);
            s_logFetchCompleted(_logger, jobId, null);

            return jobListing;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch LinkedIn job details for jobId={JobId}", jobId);
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
        var scrollSteps = new[] { 400, 800, 1200, 1600, 2000 };

        foreach (var scrollY in scrollSteps)
        {
            await page.EvaluateAsync<object>($"() => window.scrollTo({{ top: {scrollY}, behavior: 'smooth' }})", null, ct);

            // Variable delay between 800-2000ms
            var delayMs = Random.Shared.Next(800, 2000);
            await Task.Delay(delayMs, ct);
        }

        // Scroll back up slightly (human behavior)
        await page.EvaluateAsync<object>("() => window.scrollTo({ top: 600, behavior: 'smooth' })", null, ct);
        await Task.Delay(Random.Shared.Next(800, 1500), ct);

        // Final scroll down
        await page.EvaluateAsync<object>("() => window.scrollTo({ top: 2400, behavior: 'smooth' })", null, ct);
        await Task.Delay(Random.Shared.Next(1000, 2000), ct);
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
