using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Ghost.Contracts.Jobs;
using Ghost.Http;
using Ghost.Platform.Common.Session;
using Polly;

namespace Ghost.Platform.Google.Jobs.Internal;

public sealed class GoogleJobsApiClient : IDisposable
{
    private readonly HttpClient? _http;
    private readonly ISessionOrchestrator? _sessionOrchestrator;
    private readonly GoogleJobsOptions _options;
    private readonly ILogger<GoogleJobsApiClient> _logger;
    private readonly CookieContainer _cookieContainer;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private bool _disposed;
    private string? _currentSessionId;

    private static readonly Action<ILogger, string, Exception?> LogFetchingJobs =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(LogFetchingJobs)), "Fetching Google Jobs from: {Url}");

    private static readonly Action<ILogger, string, Exception?> LogReceivedEmptyHtml =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, nameof(LogReceivedEmptyHtml)), "Received empty HTML content from Google for url {Url}");

    private static readonly Action<ILogger, int, Exception?> LogReceivedHtml =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(LogReceivedHtml)), "Received HTML content: {Length} bytes");

    private static readonly Action<ILogger, string, Exception?> LogReceivedEmptyAsyncBody =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, nameof(LogReceivedEmptyAsyncBody)), "Received empty async body from {AsyncUrl}");

    private static readonly Action<ILogger, int, Exception?> LogReceivedAsyncBody =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(5, nameof(LogReceivedAsyncBody)), "Received async body: {Length} bytes");

    private static readonly Action<ILogger, string, Exception?> LogSendingAsyncRequest =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(6, nameof(LogSendingAsyncRequest)), "Sending async pagination request: {Url}");

    private static readonly Action<ILogger, string, string, Exception?> LogTryingProxy =
        LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(7, nameof(LogTryingProxy)), "Trying proxy {Proxy} for url {Url}");

    private static readonly Action<ILogger, string, string, Exception?> LogProxyFailed =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(8, nameof(LogProxyFailed)), "Proxy {Proxy} failed for {Url}");

    private static readonly Action<ILogger, string, Exception?> LogConsentPageDetected =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(9, nameof(LogConsentPageDetected)), "Consent page detected for query: {Query}");

    private static readonly Action<ILogger, int, Exception?> LogJobsFound =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(10, nameof(LogJobsFound)), "Google Jobs search completed. Found {Count} jobs");

    private static readonly Action<ILogger, string, Exception?> LogCursorExtracted =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(11, nameof(LogCursorExtracted)), "Extracted pagination cursor: {Cursor}");

    private static readonly Action<ILogger, Exception?> LogCursorNotFound =
        LoggerMessage.Define(LogLevel.Debug, new EventId(12, nameof(LogCursorNotFound)), "No pagination cursor found in response");

    private static readonly Action<ILogger, string, Exception?> LogHtmlPreview =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(13, nameof(LogHtmlPreview)), "HTML Preview (first 500 chars): {Preview}");

    private static readonly Action<ILogger, int, int, Exception?> LogParserResults =
        LoggerMessage.Define<int, int>(LogLevel.Information, new EventId(14, nameof(LogParserResults)), "Parser iteration {Iteration}: Found {Count} jobs");

    private static readonly Action<ILogger, Exception?> LogCookieInjection =
        LoggerMessage.Define(LogLevel.Debug, new EventId(15, nameof(LogCookieInjection)), "Injecting consent bypass cookies into HTTP request");

    private static readonly Action<ILogger, string, Exception?> LogUserAgentRotation =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(16, nameof(LogUserAgentRotation)), "Using user agent: {UserAgent}");

    private static readonly Action<ILogger, string, Exception?> LogSessionAllocated =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(17, "SessionAllocated"), "Allocated session {SessionId} for Google Jobs requests");

    private static readonly Action<ILogger, string, Exception?> LogSessionRecycled =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(18, "SessionRecycled"), "Recycling unhealthy session {SessionId}");

    private static readonly Action<ILogger, string, Exception?> LogSessionGetFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(19, "SessionGetFailed"), "Failed to get HTTP session {SessionId}");

    private static readonly Action<ILogger, string, Exception?> LogSessionHealthCheckFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(20, "SessionHealthCheckFailed"), "Failed to check session health for {SessionId}");

    private static readonly Action<ILogger, string, Exception?> LogSessionCloseFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(21, "SessionCloseFailed"), "Failed to close session {SessionId} during disposal");

    // Track request count if needed for session rotation logic. Suppress unused/analysis warnings for now.
    #pragma warning disable CS0169, CS0414, CA1805
    private int _requestCount;
    #pragma warning restore CS0169, CS0414, CA1805
    private const int MaxRequestsPerSession = 5;

    // Additional existing eventId gap avoided


    /// <summary>
    /// Legacy constructor for backward compatibility. Uses direct HttpClient.
    /// </summary>
    public GoogleJobsApiClient(HttpClient http, GoogleJobsOptions options, ILogger<GoogleJobsApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _sessionOrchestrator = null;
        _options = options ?? new GoogleJobsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cookieContainer = new CookieContainer();
        _retryPolicy = EnhancedRetryPolicy.CreatePolicy(logger, maxRetries: 3, enableJitter: true);
    }

    /// <summary>
    /// Modern constructor with SessionOrchestrator support for session continuity and health monitoring.
    /// </summary>
    public GoogleJobsApiClient(ISessionOrchestrator sessionOrchestrator, GoogleJobsOptions options, ILogger<GoogleJobsApiClient> logger)
    {
        _http = null;
        _sessionOrchestrator = sessionOrchestrator ?? throw new ArgumentNullException(nameof(sessionOrchestrator));
        _options = options ?? new GoogleJobsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cookieContainer = new CookieContainer();
        _retryPolicy = EnhancedRetryPolicy.CreatePolicy(logger, maxRetries: 3, enableJitter: true);
    }

        public async Task<IReadOnlyList<JobListing>> SearchAsync(string query, string location)
        {
            if (_sessionOrchestrator != null)
            {
                return await SearchWithOrchestratorAsync(query, location);
            }
            else
            {
                return await SearchLegacyAsync(query, location);
            }
        }

        private async Task<IReadOnlyList<JobListing>> SearchWithOrchestratorAsync(string query, string location)
        {
            var affinityKey = $"google_jobs_{query}_{location}_{Guid.NewGuid():N}";

            try
            {
                var context = new SessionAllocationContext(
                    PlatformName: "GoogleJobs",
                    CountryCode: "US",
                    SessionType: SessionType.Http,
                    ComplexityScore: 40,
                    Metadata: new Dictionary<string, string>
                    {
                        ["Query"] = query,
                        ["Location"] = location
                    }
                );

                var affinityOptions = new SessionAffinityOptions(
                    AffinityKey: affinityKey,
                    AffinityDuration: TimeSpan.FromMinutes(5),
                    AllowFallback: true
                );

                _currentSessionId = await _sessionOrchestrator!.AllocateSessionWithAffinityAsync(context, affinityOptions, default);
                LogSessionAllocated(_logger, _currentSessionId, null);

                var httpSession = await _sessionOrchestrator.GetHttpSessionAsync(_currentSessionId, default);
                if (httpSession == null)
                {
                    LogSessionGetFailed(_logger, _currentSessionId, null);
                    return Array.Empty<JobListing>();
                }

                return await ExecuteSearchAsync(query, location, httpSession);
            }
            finally
            {
                if (_currentSessionId != null)
                {
                    await _sessionOrchestrator!.CloseSessionAsync(_currentSessionId, default);
                    _currentSessionId = null;
                }
            }
        }

        private async Task<IReadOnlyList<JobListing>> SearchLegacyAsync(string query, string location)
        {
            return await ExecuteSearchAsync(query, location, null);
        }

        private async Task<IReadOnlyList<JobListing>> ExecuteSearchAsync(string query, string location, RotatingProxySession? httpSession)
        {
            var q = System.Uri.EscapeDataString(query);
            var loc = System.Uri.EscapeDataString(location);
        // Append async bootstrap parameter to help bypass consent pages (JobSpy technique)
        // Use AsyncBootstrapString from GoogleJobsConstants and ensure it's URL-encoded
        var asyncParam = Uri.EscapeDataString(GoogleJobsConstants.AsyncBootstrapString);
        // Try using additional parameters to bypass consent: pws=0 (disable personalization), filter=0 (show all results)
        var url = $"https://www.google.com/search?q={q}+{loc}&ibp=htl;jobs&udm=8&gl=us&hl=en&hl=en-US&async={asyncParam}&pws=0&filter=0";

        LogFetchingJobs(_logger, url, null);

        LogCookieInjection(_logger, null);
        var userAgent = GoogleJobsConstants.GetRandomUserAgent();
        LogUserAgentRotation(_logger, userAgent, null);

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var header in GoogleJobsConstants.SearchHeaders)
        {
            req.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        // Override User-Agent with rotated value
        req.Headers.TryAddWithoutValidation("User-Agent", userAgent);

        // Add consent bypass cookies
        var cookieValue = $"{GoogleJobsConstants.ConsentCookie}; {GoogleJobsConstants.SocsCookie}";
        req.Headers.TryAddWithoutValidation("Cookie", cookieValue);

        HttpResponseMessage res;
        if (httpSession != null)
        {
            res = await _retryPolicy.ExecuteAsync(async () => await httpSession.ExecuteAsync(() => req, default).ConfigureAwait(false)).ConfigureAwait(false);
        }
        else
        {
            res = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(req).ConfigureAwait(false)).ConfigureAwait(false);
        }
        var html = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

        // DEBUG: Write raw HTML to file
        try { System.IO.File.WriteAllText("logs/google_jobs_search.html", html); } catch { }

        // Enhanced consent detection - check for multiple Google consent patterns
        bool isConsentPage = html.Contains("consent.google.com") || 
                             html.Contains("Before you continue to Google Search") ||
                             html.Contains("We need to verify you're human") ||
                             html.Contains("Checking if the site connection is secure") ||
                             html.Contains("www.google.com/sorry/index") ||
                             html.Contains("distil_r_captcha") ||
                             html.Contains("g-recaptcha") ||
                             html.Contains("cf_chl_");
        
        if (isConsentPage)
        {
            LogConsentPageDetected(_logger, query, null);
            LogFetchingJobs(_logger, "Detected consent page, trying alternative approaches...", null);
            
            var alternativeUrls = new[]
            {
                $"https://www.google.com/search?q={q}+{loc}&ibp=htl%3Bjobs&udm=8&gl=us&hl=en&hl=en-US&tbs=qdr:d",
                $"https://www.google.com/search?q={q}+{loc}&ibp=htl%3Bjobs&udm=8&gl=us&hl=en&hl=en-US&tbs=qdr:w",
                $"https://www.google.com/search?q={q}+{loc}&ibp=htl%3Bjobs&udm=8&gl=us&hl=en&hl=en-US&tbs=qdr:m",
                $"https://www.google.com/search?q={q}+{loc}&ibp=htl%3Bjobs&udm=8&gl=us&hl=en&hl=en-US&source=hp",
                $"https://www.google.co.uk/search?q={q}+{loc}&ibp=htl%3Bjobs&udm=8&gl=uk&hl=en",
                $"https://www.google.ca/search?q={q}+{loc}&ibp=htl%3Bjobs&udm=8&gl=ca&hl=en",
                $"https://www.google.com.au/search?q={q}+{loc}&ibp=htl%3Bjobs&udm=8&gl=au&hl=en",
            };
            
            foreach (var altUrl in alternativeUrls)
            {
                LogFetchingJobs(_logger, $"Trying alternative URL: {altUrl}", null);
                await Task.Delay(2000); // Wait longer between retries

                var retryUserAgent = GoogleJobsConstants.GetRandomUserAgent();
                LogUserAgentRotation(_logger, retryUserAgent, null);

                var retryReq = new HttpRequestMessage(HttpMethod.Get, altUrl);
                foreach (var header in GoogleJobsConstants.SearchHeaders)
                {
                    retryReq.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                retryReq.Headers.TryAddWithoutValidation("User-Agent", retryUserAgent);
                retryReq.Headers.TryAddWithoutValidation("Cookie", cookieValue);
                
                try
                {
                    HttpResponseMessage retryRes;
                    if (httpSession != null)
                    {
                        retryRes = await _retryPolicy.ExecuteAsync(async () => await httpSession.ExecuteAsync(() => retryReq, default).ConfigureAwait(false)).ConfigureAwait(false);
                    }
                    else
                    {
                        retryRes = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(retryReq).ConfigureAwait(false)).ConfigureAwait(false);
                    }
                    html = await retryRes.Content.ReadAsStringAsync().ConfigureAwait(false);
                    
                    try { System.IO.File.WriteAllText($"logs/google_jobs_search_retry_{DateTime.Now.Ticks}.html", html); } catch { }
                    
                    // Check if this attempt succeeded
                    isConsentPage = html.Contains("consent.google.com") || 
                                   html.Contains("Before you continue to Google Search") ||
                                   html.Contains("We need to verify you're human") ||
                                   html.Contains("Checking if the site connection is secure") ||
                                   html.Contains("www.google.com/sorry/index") ||
                                   html.Contains("distil_r_captcha") ||
                                   html.Contains("g-recaptcha") ||
                                   html.Contains("cf_chl_");
                    
                if (!isConsentPage)
                {
                        LogFetchingJobs(_logger, $"Successfully bypassed consent page with alternative URL", null);
                        // log a preview of the html for debugging
                        var preview = html.Length > 500 ? html.Substring(0, 500) : html;
                        LogHtmlPreview(_logger, preview, null);
                        break; // Success! Use this HTML
                    }
                }
                catch (Exception ex)
                {
                    LogFetchingJobs(_logger, $"Error trying alternative URL {altUrl}: {ex.Message}", ex);
                }
            }
            
            // If still consent page after all attempts, return empty results
            if (isConsentPage)
            {
                LogFetchingJobs(_logger, "All consent bypass attempts failed, returning empty results", null);
                return Array.Empty<JobListing>();
            }
        }

        if (string.IsNullOrEmpty(html))
        {
            LogReceivedEmptyHtml(_logger, url, null);
            return Array.Empty<JobListing>();
        }

        LogReceivedHtml(_logger, html.Length, null);

        var cursorMatch = Regex.Match(html, GoogleJobsConstants.DataAsyncFcRegex);
        var cursor = cursorMatch.Success ? cursorMatch.Groups["cursor"].Value : null;
        if (!string.IsNullOrEmpty(cursor))
        {
            LogCursorExtracted(_logger, cursor, null);
        }
        else
        {
            LogCursorNotFound(_logger, null);
        }

        var results = new List<JobListing>();
        var initialParsed = GoogleJobsParser.ParseFromHtml(html, _logger);
        LogParserResults(_logger, 1, initialParsed.Count, null);
        if (initialParsed.Count == 0)
        {
            var preview = html.Length > 500 ? html.Substring(0, 500) : html;
            LogHtmlPreview(_logger, preview, null);
        }
        results.AddRange(initialParsed);

        int rounds = 0;
        while (!string.IsNullOrEmpty(cursor) && rounds++ < 5)
        {
                var asyncUrl = $"https://www.google.com/async/callback:550?fc={Uri.EscapeDataString(cursor)}&fcv=3&async={Uri.EscapeDataString(_options.AsyncBootstrapString)}";
            LogSendingAsyncRequest(_logger, asyncUrl, null);

            var asyncUserAgent = GoogleJobsConstants.GetRandomUserAgent();
            LogUserAgentRotation(_logger, asyncUserAgent, null);

            var asyncReq = new HttpRequestMessage(HttpMethod.Get, asyncUrl);
            foreach (var header in GoogleJobsConstants.AsyncHeaders)
            {
                asyncReq.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            asyncReq.Headers.TryAddWithoutValidation("User-Agent", asyncUserAgent);
            asyncReq.Headers.TryAddWithoutValidation("Cookie", cookieValue);

            HttpResponseMessage asyncRes;
            if (httpSession != null)
            {
                asyncRes = await _retryPolicy.ExecuteAsync(async () => await httpSession.ExecuteAsync(() => asyncReq, default).ConfigureAwait(false)).ConfigureAwait(false);
            }
            else
            {
                asyncRes = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(asyncReq).ConfigureAwait(false)).ConfigureAwait(false);
            }
            var body = await asyncRes.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (string.IsNullOrEmpty(body))
            {
                LogReceivedEmptyAsyncBody(_logger, asyncUrl, null);
            }
            else
            {
                LogReceivedAsyncBody(_logger, body.Length, null);
            }

            var parsed = GoogleJobsParser.ParseFromHtml(body, _logger);
            LogParserResults(_logger, rounds + 1, parsed.Count, null);
            if (parsed.Count == 0)
            {
                var preview = body.Length > 500 ? body.Substring(0, 500) : body;
                LogHtmlPreview(_logger, preview, null);
            }
            results.AddRange(parsed);

            var nextCursorMatch = Regex.Match(body, GoogleJobsConstants.DataAsyncFcRegex);
            cursor = nextCursorMatch.Success ? nextCursorMatch.Groups["cursor"].Value : null;

            await Task.Delay(300).ConfigureAwait(false);
        }

        LogJobsFound(_logger, results.Count, null);

        _requestCount++;
        if (_requestCount >= MaxRequestsPerSession)
        {
            _requestCount = 0;
        }

        return results;
    }

    private async Task CheckAndRecycleSessionAsync()
    {
        if (_sessionOrchestrator == null || _currentSessionId == null)
        {
            return;
        }

        try
        {
            var health = await _sessionOrchestrator.GetSessionHealthAsync(_currentSessionId, default);
            if (health.Health == SessionHealth.Unhealthy)
            {
                LogSessionRecycled(_logger, _currentSessionId, null);
                await _sessionOrchestrator.RecycleSessionAsync(_currentSessionId, default);
                _currentSessionId = null;
            }
        }
        catch (Exception ex)
        {
            LogSessionHealthCheckFailed(_logger, _currentSessionId ?? "unknown", ex);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            if (_currentSessionId != null && _sessionOrchestrator != null)
            {
                try
                {
                    _sessionOrchestrator.CloseSessionAsync(_currentSessionId, default).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    LogSessionCloseFailed(_logger, _currentSessionId, ex);
                }
            }

            _disposed = true;
        }
    }
}
