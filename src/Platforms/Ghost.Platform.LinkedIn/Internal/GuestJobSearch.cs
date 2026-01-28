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

namespace Ghost.Platform.LinkedIn.Internal;

public sealed class GuestJobSearch
{
    private readonly GhostKernel _kernel;
    private readonly IProxyProvider _proxyProvider;
    private readonly ILogger<GuestJobSearch> _logger;
    private readonly LinkedInOptions _options = new();

    private static readonly Action<ILogger, string, Exception?> s_logUsingProxy =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(GuestJobSearch)), "Using proxy: {Proxy}");

    private static readonly Action<ILogger, string, Exception?> s_logNavigating =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, nameof(GuestJobSearch)), "Navigating to: {Url}");

    private static readonly Action<ILogger, Exception?> s_logGuestSearchFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(GuestJobSearch)), "Guest search navigation/parsing failed");

    private static readonly Action<ILogger, Exception?> s_logProxyDisabled =
        LoggerMessage.Define(LogLevel.Information, new EventId(4, nameof(GuestJobSearch)), "Proxy disabled by configuration. Using direct connection.");

    public GuestJobSearch(
        GhostKernel kernel,
        IProxyProvider proxyProvider,
        IOptions<LinkedInOptions> options,
        ILogger<GuestJobSearch> logger)
    {
        ArgumentNullException.ThrowIfNull(kernel);
        ArgumentNullException.ThrowIfNull(proxyProvider);
        _kernel = kernel;
        _proxyProvider = proxyProvider;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GuestJobSearch>.Instance;
        if (options?.Value is not null)
        {
            _options = options.Value;
        }
    }

        public async Task<IReadOnlyList<string>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct)
        {
        ArgumentNullException.ThrowIfNull(criteria);

        var ids = new List<string>();
        // Create a fresh session for the search to isolate from other work
        SessionOptions options;
        if (!_options.ProxyEnabled)
        {
            // When proxy usage is disabled by configuration, do not fetch a proxy
            // and create session options without proxy settings.
            s_logProxyDisabled(_logger, null);
            options = new SessionOptions { Proxy = null };
        }
        else
        {
            // Keep existing behavior: fetch a proxy and apply settings to the session
            var proxy = _proxyProvider is not null ? await _proxyProvider.GetProxyAsync("US", ct) : null;
            // log the proxy being used for this search
            s_logUsingProxy(_logger, proxy?.Server ?? "None", null);
            options = new SessionOptions();
            if (proxy is not null)
            {
                options.Proxy = new SessionOptions.ProxySettings(proxy.Server, proxy.Username, proxy.Password);
            }
        }

        var session = await _kernel.NewSessionAsync(options, ct);
        var page = await session.NewPageAsync(ct: ct);
        try
        {
            var q = Uri.EscapeDataString(criteria.Query ?? string.Empty);
            var loc = Uri.EscapeDataString(criteria.Location ?? string.Empty);

            for (var offset = 0; ids.Count < limit; offset += 25)
            {
                ct.ThrowIfCancellationRequested();
                var url = $"{_options.BaseUrl}/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords={q}&location={loc}&start={offset}";
            try
            {
                s_logNavigating(_logger, url, null);
                await page.NavigateAsync(url, ct: ct);
                // no full load expected - just get content
                var html = await page.GetContentAsync(ct);
                    if (string.IsNullOrEmpty(html)) break;

                    // 429 handling: LinkedIn sometimes returns a 429 message in the HTML
                    if (html.Contains("429 Too Many Requests", StringComparison.OrdinalIgnoreCase) || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                    {
                        LinkedInLogGuest.LogGuestApiThrottled(_logger);
                        break;
                    }

                    var found = ExtractIdsFromSearchHtml(html);
                    if (found.Count == 0) break;

                    foreach (var id in found)
                    {
                        if (ids.Count >= limit) break;
                        if (!ids.Contains(id)) ids.Add(id);
                    }
                    // if fewer than page size returned, stop
                    if (found.Count < 25) break;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // log exception details for diagnosis
                    s_logGuestSearchFailed(_logger, ex);
                    LinkedInLog.LogFailedToParseSearchNode(_logger, ex);
                    break;
                }
            }

            return ids;
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

    public async Task<JobListing?> FetchJobDetailsAsync(string jobId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        // create a fresh proxied session for fetching job details
        var proxy = _proxyProvider is not null ? await _proxyProvider.GetProxyAsync("US", ct) : null;
        var options = new SessionOptions();
        if (proxy is not null)
        {
            options.Proxy = new SessionOptions.ProxySettings(proxy.Server, proxy.Username, proxy.Password);
        }

        var session = await _kernel.NewSessionAsync(options, ct);
        var page = await session.NewPageAsync(ct: ct);
        try
        {
            var url = $"{_options.BaseUrl}/jobs-guest/jobs/api/jobPosting/{jobId}";
            try
            {
                await page.NavigateAsync(url, ct: ct);
                var html = await page.GetContentAsync(ct);
                if (string.IsNullOrEmpty(html)) return null;

                if (html.Contains("429", StringComparison.OrdinalIgnoreCase) || html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                {
                    LinkedInLogGuest.LogGuestJobEndpointThrottled(_logger, jobId);
                    return null;
                }

                var parsed = JsonLdParser.Parse(html, jobId, url);
                return parsed;
            }
            catch (OperationCanceledException) { throw; }
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
