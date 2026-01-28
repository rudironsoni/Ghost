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

namespace Ghost.Platform.LinkedIn.Internal;

public sealed class GuestJobSearch
{
    private readonly GhostKernel _kernel;
    private readonly IProxyProvider _proxyProvider;
    private readonly ILogger<GuestJobSearch> _logger;
    private readonly IOptions<LinkedInOptions> _options;
    private readonly LinkedInAuthenticator _authenticator;

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

    public GuestJobSearch(
        GhostKernel kernel,
        IProxyProvider proxyProvider,
        IOptions<LinkedInOptions> options,
        LinkedInAuthenticator authenticator,
        ILogger<GuestJobSearch> logger)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(proxyProvider);
        _kernel = kernel;
        _proxyProvider = proxyProvider;
        _authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GuestJobSearch>.Instance;
        _options = options ?? throw new ArgumentNullException(nameof(options));
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
            var baseUrl = $"{_options.Value.BaseUrl}/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords={q}&location={loc}&start={offset}";
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
        var url = $"{_options.Value.BaseUrl}/jobs-guest/jobs/api/jobPosting/{jobId}";
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
                    var html = await page.GetContentAsync(ct);
                    if (string.IsNullOrEmpty(html)) return null;

                    if (html.Contains("429", StringComparison.OrdinalIgnoreCase) || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                    {
                        LinkedInLogGuest.LogGuestJobEndpointThrottled(_logger, jobId);
                        return null;
                    }

                    var parsed = JsonLdParser.Parse(html, jobId, url);

                    // If JSON-LD parsing failed to extract a description, fall back to DOM scraping
                    if (parsed is null || string.IsNullOrEmpty(parsed.Description))
                    {
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

                        var descSelectors = new[] { ".show-more-less-html__markup", ".description__text", "#job-details" };
                        var titleSelectors = new[] { ".top-card-layout__title", "h1" };
                        var companySelectors = new[] { ".top-card-layout__first-subline .topcard__org-name-link" };
                        var locationSelectors = new[] { ".top-card-layout__first-subline .topcard__flavor--bullet" };

                        var scrapedDescription = await ScrapeFirstAsync(page, descSelectors, ct);
                        var scrapedTitle = await ScrapeFirstAsync(page, titleSelectors, ct);
                        var scrapedCompany = await ScrapeFirstAsync(page, companySelectors, ct);
                        var scrapedLocation = await ScrapeFirstAsync(page, locationSelectors, ct);

                        if (parsed is null)
                        {
                            parsed = new JobListing
                            {
                                Id = jobId,
                                Description = scrapedDescription,
                                Title = scrapedTitle ?? string.Empty,
                                Company = scrapedCompany ?? string.Empty,
                                Location = scrapedLocation
                            };
                        }
                        else
                        {
                            // prefer parsed values, but fill missing fields from scraped values
                            var desc = string.IsNullOrWhiteSpace(parsed.Description) ? scrapedDescription : parsed.Description;
                            var title = string.IsNullOrWhiteSpace(parsed.Title) ? (scrapedTitle ?? parsed.Title) : parsed.Title;
                            var company = string.IsNullOrWhiteSpace(parsed.Company) ? (scrapedCompany ?? parsed.Company) : parsed.Company;
                            var location = string.IsNullOrWhiteSpace(parsed.Location) ? scrapedLocation : parsed.Location;

                            parsed = parsed with
                            {
                                Id = jobId,
                                Description = desc,
                                Title = title,
                                Company = company,
                                Location = location
                            };
                        }
                    }

                    return parsed;
                }
                catch (OperationCanceledException) { throw; }
                catch (PlaywrightException pex)
                {
                    // Treat Playwright errors as proxy/session failures and retry with a new proxy/session
                    s_logProxyFailed(_logger, attemptProxy, attempt, pex.Message, null);
                    try { await page.DisposeAsync(); } catch { }
                    try { await session.DisposeAsync(); } catch { }
                    // continue to next attempt
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

        // If we get here all attempts failed
        s_logAllProxyFailed(_logger, url, null);
        return null;
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
