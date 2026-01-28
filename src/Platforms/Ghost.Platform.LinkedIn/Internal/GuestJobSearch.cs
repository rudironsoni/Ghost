using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.LinkedIn.Internal;

public sealed class GuestJobSearch
{
    private readonly Ghost.IBrowserSession _session;
    private readonly ILogger<GuestJobSearch> _logger;
    private readonly LinkedInOptions _options = new();

    public GuestJobSearch(Ghost.IBrowserSession session, ILogger<GuestJobSearch> logger)
    {
        ArgumentNullException.ThrowIfNull(session);
        _session = session;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<GuestJobSearch>.Instance;
    }

    public async Task<IReadOnlyList<string>> SearchAsync(JobSearchCriteria criteria, int limit, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var ids = new List<string>();
        var page = await _session.NewPageAsync(ct: ct);
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
                    LinkedInLog.LogFailedToParseSearchNode(_logger, ex);
                    break;
                }
            }

            return ids;
        }
        finally
        {
            try { await page.DisposeAsync(); } catch { }
        }
    }

    public async Task<JobListing?> FetchJobDetailsAsync(string jobId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        var page = await _session.NewPageAsync(ct: ct);
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
