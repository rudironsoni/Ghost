using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.Google.Jobs.Internal;

public sealed class GoogleJobsApiClient
{
    private readonly HttpClient _http;
    private readonly GoogleJobsOptions _options;
    private readonly ILogger<GoogleJobsApiClient> _logger;
    private readonly CookieContainer _cookieContainer;

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

    // Additional existing eventId gap avoided


    public GoogleJobsApiClient(HttpClient http, GoogleJobsOptions options, ILogger<GoogleJobsApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _options = options ?? new GoogleJobsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cookieContainer = new CookieContainer();
    }

        public async Task<IReadOnlyList<JobListing>> SearchAsync(string query, string location)
        {
            var q = System.Uri.EscapeDataString(query);
            var loc = System.Uri.EscapeDataString(location);
        // Append async bootstrap parameter to help bypass consent pages (JobSpy technique)
        // Use AsyncBootstrapString from GoogleJobsConstants and ensure it's URL-encoded
        var asyncParam = Uri.EscapeDataString(GoogleJobsConstants.AsyncBootstrapString);
        // Try using additional parameters to bypass consent: pws=0 (disable personalization), filter=0 (show all results)
        var url = $"https://www.google.com/search?q={q}+{loc}&ibp=htl;jobs&udm=8&gl=us&hl=en&hl=en-US&async={asyncParam}&pws=0&filter=0";

        LogFetchingJobs(_logger, url, null);

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        foreach (var header in GoogleJobsConstants.SearchHeaders)
        {
            req.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var res = await _http.SendAsync(req).ConfigureAwait(false);
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
                
                var retryReq = new HttpRequestMessage(HttpMethod.Get, altUrl);
                foreach (var header in GoogleJobsConstants.SearchHeaders)
                {
                    retryReq.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }
                
                try
                {
                    var retryRes = await _http.SendAsync(retryReq).ConfigureAwait(false);
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

            var asyncReq = new HttpRequestMessage(HttpMethod.Get, asyncUrl);
            foreach (var header in GoogleJobsConstants.AsyncHeaders)
            {
                asyncReq.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var asyncRes = await _http.SendAsync(asyncReq).ConfigureAwait(false);
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
        return results;
    }
}
