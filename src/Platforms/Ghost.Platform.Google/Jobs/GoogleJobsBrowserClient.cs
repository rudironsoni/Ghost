using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Consent;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Google.Jobs.Internal;
using Ghost.Session;
using Ghost.Stealth.Behavior;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Playwright = Microsoft.Playwright;

namespace Ghost.Platform.Google.Jobs;

/// <summary>
/// Enhanced Google Jobs browser client with full stealth, consent handling, and session persistence.
/// Implements heavy anti-bot detection countermeasures.
/// </summary>
public sealed class GoogleJobsBrowserClient : IAsyncDisposable
{
    private readonly Playwright.IBrowserContext _context;
    private readonly IConsentHandler? _consentHandler;
    private readonly BehavioralMimicryService? _behaviorService;
    private readonly ISessionManager? _sessionManager;
    private readonly GoogleJobsOptions _options;
    private readonly ILogger<GoogleJobsBrowserClient> _logger;
    private Playwright.IPage? _currentPage;
    private int _requestCount;
    private const string PlatformName = "GoogleJobs";

    private static readonly Action<ILogger, string, string, Exception?> LogSearchStarted =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(4001, "GoogleSearchStarted"), "Starting Google Jobs search: query='{Query}', location='{Location}'");

    private static readonly Action<ILogger, string, Exception?> LogNavigatingToUrl =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4002, "NavigatingToUrl"), "Navigating to: {Url}");

    private static readonly Action<ILogger, Exception?> LogConsentDetected =
        LoggerMessage.Define(LogLevel.Information, new EventId(4003, "ConsentDetected"), "Consent dialog detected, attempting to handle");

    private static readonly Action<ILogger, bool, Exception?> LogConsentHandled =
        LoggerMessage.Define<bool>(LogLevel.Information, new EventId(4004, "ConsentHandled"), "Consent dialog handled: success={Success}");

    private static readonly Action<ILogger, int, Exception?> LogJobsExtracted =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(4005, "JobsExtracted"), "Extracted {Count} jobs from Google");

    private static readonly Action<ILogger, Exception?> LogSessionRestored =
        LoggerMessage.Define(LogLevel.Information, new EventId(4006, "SessionRestored"), "Restored previous Google Jobs session");

    private static readonly Action<ILogger, Exception?> LogSessionSaved =
        LoggerMessage.Define(LogLevel.Information, new EventId(4007, "SessionSaved"), "Saved Google Jobs session for reuse");

    public GoogleJobsBrowserClient(
        Playwright.IBrowserContext context,
        IOptions<GoogleJobsOptions> options,
        ILogger<GoogleJobsBrowserClient> logger,
        IConsentHandler? consentHandler = null,
        BehavioralMimicryService? behaviorService = null,
        ISessionManager? sessionManager = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _options = options?.Value ?? new GoogleJobsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consentHandler = consentHandler;
        _behaviorService = behaviorService;
        _sessionManager = sessionManager;
    }

    /// <summary>
    /// Searches for jobs on Google Jobs with full stealth and consent handling.
    /// </summary>
    public async Task<IReadOnlyList<JobListing>> SearchAsync(
        string query,
        string location,
        int maxResults,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        LogSearchStarted(_logger, query, location ?? "Remote", null);

        // Restore session if available
        await RestoreSessionIfAvailableAsync(ct);

        // Create or reuse page
        _currentPage ??= await _context.NewPageAsync();

        try
        {
            var jobs = new List<JobListing>();

            // Build search URL
            var searchQuery = Uri.EscapeDataString($"{query} jobs {location}".Trim());
            var url = $"https://www.google.com/search?q={searchQuery}&ibp=htl;jobs";

            LogNavigatingToUrl(_logger, url, null);

            // Navigate with human-like behavior (slower, more realistic)
            if (_behaviorService != null)
            {
                await _behaviorService.NavigateHumanLikeAsync(_currentPage, url, ct);
            }
            else
            {
                await _currentPage.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });
                await Task.Delay(TimeSpan.FromSeconds(3), ct); // Manual delay if no behavior service
            }

            // Handle consent dialogs (critical for Google)
            await HandleConsentAsync(ct);

            // Wait for job listings to load
            await WaitForJobListingsAsync(ct);

            // Extract jobs from page
            jobs = await ExtractJobsFromPageAsync(maxResults, ct);

            LogJobsExtracted(_logger, jobs.Count, null);

            // Save session for reuse
            await SaveSessionAsync(ct);

            _requestCount++;

            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during Google Jobs search");
            throw;
        }
    }

    /// <summary>
    /// Handles consent dialogs using the consent handler (if available).
    /// </summary>
    private async Task HandleConsentAsync(CancellationToken ct)
    {
        if (_consentHandler == null || _currentPage == null)
        {
            return;
        }

        try
        {
            var cmpType = await _consentHandler.DetectCMPAsync(_currentPage);
            if (cmpType != null)
            {
                LogConsentDetected(_logger, null);
                var success = await _consentHandler.HandleConsentAsync(_currentPage);
                LogConsentHandled(_logger, success, null);

                if (success && _behaviorService != null)
                {
                    // Wait after consent with human-like delay
                    await _behaviorService.Timing.NavigationDelayAsync(ct);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to handle consent dialog, continuing anyway");
        }
    }

    /// <summary>
    /// Waits for job listings to appear on the page with multiple fallback selectors.
    /// </summary>
    private async Task WaitForJobListingsAsync(CancellationToken ct)
    {
        if (_currentPage == null)
        {
            return;
        }

        var selectors = new[]
        {
            "div[data-ved*='job']",
            ".g div[data-sokoban-container]",
            "li[data-ve-type='job']",
            ".job-listing",
            "div.PwjeAc", // Google Jobs container class (subject to change)
            "div.gws-plugins-horizon-jobs__job-card-container"
        };

        foreach (var selector in selectors)
        {
            try
            {
                await _currentPage.WaitForSelectorAsync(selector, new() { Timeout = 5000, State = WaitForSelectorState.Visible });
                return; // Success
            }
            catch
            {
                // Try next selector
            }
        }

        // If no selector worked, wait for generic content load
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
    }

    /// <summary>
    /// Extracts job listings from the current page.
    /// </summary>
    private async Task<List<JobListing>> ExtractJobsFromPageAsync(int maxResults, CancellationToken ct)
    {
        if (_currentPage == null)
        {
            return new List<JobListing>();
        }

        var html = await _currentPage.ContentAsync();
        var parser = new GoogleJobsMultiStrategyParser(_logger);
        return await parser.ParseHtmlAsync(html);
    }

    /// <summary>
    /// Restores a previous session if available.
    /// </summary>
    private async Task RestoreSessionIfAvailableAsync(CancellationToken ct)
    {
        if (_sessionManager == null)
        {
            return;
        }

        try
        {
            var restored = await _sessionManager.RestoreSessionAsync(_context, PlatformName, null, ct);
            if (restored)
            {
                LogSessionRestored(_logger, null);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to restore session, starting fresh");
        }
    }

    /// <summary>
    /// Saves the current session for reuse.
    /// </summary>
    private async Task SaveSessionAsync(CancellationToken ct)
    {
        if (_sessionManager == null)
        {
            return;
        }

        try
        {
            await _sessionManager.SaveSessionAsync(
                _context,
                PlatformName,
                sessionId: null,
                ttl: TimeSpan.FromHours(4), // Google sessions expire faster
                ct: ct);

            LogSessionSaved(_logger, null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save session");
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_currentPage != null)
        {
            await _currentPage.CloseAsync();
            _currentPage = null;
        }
    }
}
