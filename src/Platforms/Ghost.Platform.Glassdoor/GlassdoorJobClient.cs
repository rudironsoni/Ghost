using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

namespace Ghost.Platform.Glassdoor;

public sealed class GlassdoorJobClient : Ghost.Abstractions.IJobScraper
{
    private readonly Internal.GlassdoorApiClient _api;
    private readonly Internal.GlassdoorBrowserClient _browserClient;
    private readonly GlassdoorOptions _options;
    private readonly ILogger<GlassdoorJobClient> _logger;

    private static readonly Action<ILogger, Exception?> s_logHttpFallback =
        LoggerMessage.Define(LogLevel.Information, new EventId(1, nameof(GlassdoorJobClient)), "HTTP client returned no results, falling back to browser for Glassdoor");

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
            _logger.LogDebug(ex, "Failed extracting CSRF token from Glassdoor");
            return null;
        }
    }

    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(JobSearchCriteria criteria, CancellationToken ct = default)
    {
        var payload = await _api.SearchAsync(criteria.Query ?? string.Empty, criteria.Location, null, ct);
        var jobs = Internal.GlassdoorJobParser.ParseSearchResponse(payload);

        if (jobs.Count == 0 && _options.Enabled)
        {
            s_logHttpFallback(_logger, null);
            jobs = (List<JobListing>)await _browserClient.SearchAsync(criteria, criteria.MaxResults > 0 ? criteria.MaxResults : 20, ct);
        }

        return jobs;
    }

    public Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default) => Task.FromResult(new JobListing { Id = jobId, Source = "Glassdoor" });
    public Task<JobApplication> ApplyAsync(string jobId, ApplicationDetails details, CancellationToken ct = default) => Task.FromException<JobApplication>(new NotImplementedException());
    public Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(ApplicationsFilter? filter = null, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobApplication>)Array.Empty<JobApplication>());
    public Task SaveJobAsync(string jobId, CancellationToken ct = default) => Task.CompletedTask;
    public Task<IReadOnlyList<JobListing>> GetSavedJobsAsync(CancellationToken ct = default) => Task.FromResult((IReadOnlyList<JobListing>)Array.Empty<JobListing>());
}
