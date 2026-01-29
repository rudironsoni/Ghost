using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.Google.Jobs.Internal;

public sealed class GoogleJobsApiClient
{
    private readonly HttpClient _http;
    private readonly ILogger<GoogleJobsApiClient> _logger;

    private static readonly Action<ILogger, string, Exception?> LogFetchingJobs =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(LogFetchingJobs)), "Fetching Google Jobs from: {Url}");

    private static readonly Action<ILogger, string, Exception?> LogReceivedEmptyHtml =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2, nameof(LogReceivedEmptyHtml)), "Received empty HTML content from Google for url {Url}");

    private static readonly Action<ILogger, int, Exception?> LogReceivedHtml =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(3, nameof(LogReceivedHtml)), "Received HTML content: {Length} bytes");

    private static readonly Action<ILogger, string, Exception?> LogHtmlDumped =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(4, nameof(LogHtmlDumped)), "HTML dumped to: {DumpPath}");

    private static readonly Action<ILogger, string, Exception?> LogReceivedEmptyAsyncBody =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(5, nameof(LogReceivedEmptyAsyncBody)), "Received empty async body from {AsyncUrl}");

    private static readonly Action<ILogger, int, Exception?> LogReceivedAsyncBody =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(6, nameof(LogReceivedAsyncBody)), "Received async body: {Length} bytes");


    public GoogleJobsApiClient(HttpClient http, ILogger<GoogleJobsApiClient> logger)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<JobListing>> SearchAsync(string query, string location)
    {
        var q = System.Uri.EscapeDataString(query);
        var loc = System.Uri.EscapeDataString(location);
        // Use the 'ibp=htl;jobs' parameter which targets the Google Jobs (Jobs widget) view
        // falling back to a plain search URL if needed.
        var url = $"https://www.google.com/search?q={q}+{loc}&ibp=htl;jobs";

        LogFetchingJobs(_logger, url, null);

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        var res = await _http.SendAsync(req).ConfigureAwait(false);
        var html = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrEmpty(html))
        {
            LogReceivedEmptyHtml(_logger, url, null);
        }
        else
        {
            LogReceivedHtml(_logger, html.Length, null);
            // Dump HTML to file for debugging
            var dumpPath = Path.Combine(Environment.CurrentDirectory, "google_dump.html");
            await File.WriteAllTextAsync(dumpPath, html).ConfigureAwait(false);
            LogHtmlDumped(_logger, dumpPath, null);
        }

        // Extract cursor
        var m = Regex.Match(html, GoogleJobsConstants.DataAsyncFcRegex);
        var cursor = m.Success ? m.Groups["cursor"].Value : null;

        var results = new List<JobListing>();
        results.AddRange(GoogleJobsParser.ParseFromHtml(html, _logger));

        // simple pagination loop - call async callback with cursor while available
        int rounds = 0;
        while (!string.IsNullOrEmpty(cursor) && rounds++ < 5)
        {
            var asyncUrl = $"https://www.google.com/async/callback:550?{GoogleJobsConstants.AsyncParam}={System.Uri.EscapeDataString(cursor)}";
            var r2 = await _http.GetAsync(asyncUrl).ConfigureAwait(false);
            var body = await r2.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (string.IsNullOrEmpty(body))
            {
                LogReceivedEmptyAsyncBody(_logger, asyncUrl, null);
            }
            else
            {
                LogReceivedAsyncBody(_logger, body.Length, null);
            }

            // Parse for new jobs and cursor
            results.AddRange(GoogleJobsParser.ParseFromHtml(body, _logger));
            var m2 = Regex.Match(body, GoogleJobsConstants.DataAsyncFcRegex);
            cursor = m2.Success ? m2.Groups["cursor"].Value : null;
            await Task.Delay(300).ConfigureAwait(false);
        }

        return results;
    }
}
