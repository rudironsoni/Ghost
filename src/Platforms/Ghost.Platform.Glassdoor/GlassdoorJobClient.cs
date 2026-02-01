using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace Ghost.Platform.Glassdoor;

public sealed class GlassdoorJobClient : Ghost.Abstractions.IJobScraper
{
    // CookieContainer to store session cookies and CSRF token for Glassdoor
    private readonly System.Net.CookieContainer _cookieContainer = new System.Net.CookieContainer();
    private string? _csrfToken;

    /// <summary>
    /// RefreshSession clears existing cookies and fetches a new session from Glassdoor,
    /// extracting the CSRF token and storing session cookies in the internal CookieContainer.
    /// </summary>
    public async Task RefreshSession(CancellationToken ct = default)
    {
        // Clear existing cookies
        try
        {
            // CookieContainer has no Clear method; replace with a new instance by reflection workaround
            // Simpler: create new CookieContainer and swap via local variable (this instance is readonly, so we use Add to clear)
            // Remove cookies by enumerating domains and expiring them
            // Best-effort: enumerate all cookies from known Glassdoor domains
            var domains = new[] { 
                ".glassdoor.com", "glassdoor.com", "www.glassdoor.com" 
            };
            foreach (var d in domains)
            {
                try
                {
                    var uri = new Uri($"https://{d}");
                    var cookies = _cookieContainer.GetCookies(uri);
                    foreach (System.Net.Cookie c in cookies)
                    {
                        c.Expired = true;
                    }
                }
                catch { /* ignore */ }
            }
        }
        catch { /* best-effort */ }

        // Request a fresh session page to obtain cookies and CSRF token
        try
        {
            using var handler = new System.Net.Http.HttpClientHandler { CookieContainer = _cookieContainer, AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate };
            using var client = new System.Net.Http.HttpClient(handler);
            client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; GhostBot/1.0)");
            var resp = await client.GetAsync("https://www.glassdoor.com/index.htm", ct);
            resp.EnsureSuccessStatusCode();
            var html = await resp.Content.ReadAsStringAsync(ct);

            // Try extract CSRF token using common patterns
            // Example patterns: "csrfToken":"..." or name="csrf-token" value="..."
            string? token = null;
            try
            {
                // simple search
                var marker = "csrfToken\":\"";
                var idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0)
                {
                    idx += marker.Length;
                    var end = html.IndexOf('"', idx);
                    if (end > idx)
                        token = html.Substring(idx, end - idx);
                }

                if (token == null)
                {
                    // fallback: meta tag
                    marker = "name=\"csrf-token\" value=\"";
                    idx = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                    if (idx >= 0)
                    {
                        idx += marker.Length;
                        var end = html.IndexOf('"', idx);
                        if (end > idx)
                            token = html.Substring(idx, end - idx);
                    }
                }
            }
            catch { /* ignore parsing errors */ }

            _csrfToken = token;
        }
        catch (Exception ex)
        {
            s_logRefreshSessionFailed(_logger, ex);
            throw;
        }
    }
    private readonly Internal.GlassdoorApiClient _api;
    private readonly Internal.GlassdoorBrowserClient _browserClient;
    private readonly GlassdoorOptions _options;
    private readonly ILogger<GlassdoorJobClient> _logger;

    private static readonly Action<ILogger, Exception?> s_logHttpFallback =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(GlassdoorJobClient)), "HTTP client returned no results, falling back to browser for Glassdoor");
    private static readonly Action<ILogger, Exception?> s_logRefreshSessionFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(2, "RefreshSession"), "Failed to refresh Glassdoor session");
    private static readonly Action<ILogger, Exception?> s_logCsrfExtractFailed =
        LoggerMessage.Define(LogLevel.Debug, new EventId(3, "ExtractCsrfToken"), "Failed extracting CSRF token from Glassdoor");

    public GlassdoorJobClient(
        Internal.GlassdoorApiClient api,
        Internal.GlassdoorBrowserClient browserClient,
        IOptions<GlassdoorOptions> options,
        ILogger<GlassdoorJobClient> logger)
    {
        _api = api;
        _browserClient = browserClient;
        _options = options.Value;
        _logger = logger;
    }

    public string PlatformName => "Glassdoor";

    /// <summary>
    /// Fetches the Glassdoor homepage and attempts to extract a CSRF token.
    /// Looks for common patterns such as a meta[name="csrf-token"], input[name="csrf"|"csrf-token"],
    /// or simple JavaScript assignments (csrfToken = "..."). Returns null when no token found.
    /// </summary>
    public async Task<string?> ExtractCsrfToken(CancellationToken ct = default)
    {
        try
        {
            using var http = new HttpClient();
            // Use a simple user-agent to avoid trivial bot blocks
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (compatible; GhostBot/1.0)");

            using var resp = await http.GetAsync("https://www.glassdoor.com", ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();
            var html = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // 1) <meta name="csrf-token" content="..." />
            var meta = Regex.Match(html, "<meta\\s+name=[\"']csrf-token[\"']\\s+content=[\"'](?<token>[^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase);
            if (meta.Success)
                return meta.Groups["token"].Value;

            // 2) <input ... name="csrf" value="..." /> or name="csrf-token"
            var input = Regex.Match(html, "<input[^>]*name=[\"'](?:(?:csrf(?:-token)?)|csrf)[\"'][^>]*value=[\"'](?<token>[^\"']+)[\"'][^>]*>", RegexOptions.IgnoreCase);
            if (input.Success)
                return input.Groups["token"].Value;

            // 3) JS patterns: var csrfToken = "..." or csrf_token = '...'
            var js = Regex.Match(html, "(?:csrfToken|csrf_token|CSRFToken)\\s*[:=]\\s*[\"'](?<token>[^\"']+)[\"']", RegexOptions.IgnoreCase);
            if (js.Success)
                return js.Groups["token"].Value;

            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            s_logCsrfExtractFailed(_logger, ex);
            return null;
        }
    }

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        // Primary attempt: try GraphQL API using current CSRF token
        var currentToken = _csrfToken;
        string? payload = await _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location, currentToken, ct);
        var jobs = Internal.GlassdoorJobParser.ParseSearchResponse(payload);

        // If GraphQL returned a server error (or empty) try refreshing session and retrying up to 3 times
        var attempts = 0;
        const int maxRetries = 3;

        while ((jobs.Count == 0 || payload == null) && _options.Enabled && attempts < maxRetries)
        {
            // If payload indicated a server error, refresh the session and try again with a new CSRF token
            attempts++;
            try
            {
                await RefreshSession(ct).ConfigureAwait(false);
                currentToken = _csrfToken;
            }
            catch (Exception ex)
            {
                // Log and continue - we will fall back to browser if API keeps failing
                s_logRefreshSessionFailed(_logger, ex);
            }

            payload = await _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location, currentToken, ct);
            jobs = Internal.GlassdoorJobParser.ParseSearchResponse(payload);

            if (jobs.Count > 0)
                break;
        }

        // If API still produced no results, fallback to browser search when enabled
        if ((jobs.Count == 0 || payload == null) && _options.Enabled)
        {
            s_logHttpFallback(_logger, null);
            try
            {
                jobs = (List<JobListing>)await _browserClient.SearchAsync(criteria, criteria.MaxResults > 0 ? criteria.MaxResults : 20, ct);
            }
            catch
            {
                // If browser fallback also fails, return empty list per requirement
                return Array.Empty<JobListing>();
            }
        }

        return jobs;
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default) => Task.FromResult(new JobListing { Id = jobId, Source = "Glassdoor" });
    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) => Task.FromException<JobApplication>(new NotImplementedException());
    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobApplication>)Array.Empty<JobApplication>());
    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobListing>)Array.Empty<JobListing>());
}
