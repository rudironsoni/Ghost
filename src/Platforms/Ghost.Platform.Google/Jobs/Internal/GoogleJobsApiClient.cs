using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.Google.Jobs.Internal;

public sealed class GoogleJobsApiClient
{
    private readonly HttpClient _http;

    public GoogleJobsApiClient(HttpClient http)
    {
        _http = http ?? throw new ArgumentNullException(nameof(http));
    }

    public async Task<IReadOnlyList<JobListing>> SearchAsync(string query, string location)
    {
        var q = System.Uri.EscapeDataString(query);
        var loc = System.Uri.EscapeDataString(location);
        var url = $"https://www.google.com/search?q={q}+{loc}&udm=8";

        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64)");

        var res = await _http.SendAsync(req).ConfigureAwait(false);
        var html = await res.Content.ReadAsStringAsync().ConfigureAwait(false);

        // Extract cursor
        var m = Regex.Match(html, GoogleJobsConstants.DataAsyncFcRegex);
        var cursor = m.Success ? m.Groups["cursor"].Value : null;

        var results = new List<JobListing>();
        results.AddRange(GoogleJobsParser.ParseFromHtml(html));

        // simple pagination loop - call async callback with cursor while available
        int rounds = 0;
        while (!string.IsNullOrEmpty(cursor) && rounds++ < 5)
        {
            var asyncUrl = $"https://www.google.com/async/callback:550?{GoogleJobsConstants.AsyncParam}={System.Uri.EscapeDataString(cursor)}";
            var r2 = await _http.GetAsync(asyncUrl).ConfigureAwait(false);
            var body = await r2.Content.ReadAsStringAsync().ConfigureAwait(false);

            // Parse for new jobs and cursor
            results.AddRange(GoogleJobsParser.ParseFromHtml(body));
            var m2 = Regex.Match(body, GoogleJobsConstants.DataAsyncFcRegex);
            cursor = m2.Success ? m2.Groups["cursor"].Value : null;
            await Task.Delay(300).ConfigureAwait(false);
        }

        return results;
    }
}
