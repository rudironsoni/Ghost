using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ghost.Plugin.LinkedIn.Internal;

/// <summary>
/// HTTP-based client for LinkedIn guest API that doesn't require browser automation.
/// </summary>
public sealed class LinkedInHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly LinkedInOptions _options;
    private readonly ILogger<LinkedInHttpClient> _logger;
    private readonly ICountryDomainProvider _countryProvider;

    private static readonly Action<ILogger, string, Exception?> s_logFetching =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1, nameof(LinkedInHttpClient)), "Fetching: {Url}");

    private static readonly Action<ILogger, int, Exception?> s_logJobsFound =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(2, nameof(LinkedInHttpClient)), "Found {Count} jobs via HTTP client");

    private static readonly Action<ILogger, Exception?> s_logFetchFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(LinkedInHttpClient)), "HTTP client fetch failed");

    private static readonly Action<ILogger, string, Exception?> s_logRateLimited =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(4, nameof(LinkedInHttpClient)), "Rate limited by LinkedIn: {Url}");

    private static readonly Action<ILogger, Exception?> s_logParseJobCardFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(5, nameof(LinkedInHttpClient)), "Failed to parse job card from HTML");

    private static readonly Action<ILogger, Exception?> s_logParseJobDetailsFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(6, nameof(LinkedInHttpClient)), "Failed to parse job details from HTML");

    public LinkedInHttpClient(
        HttpClient httpClient,
        IOptions<LinkedInOptions> options,
        ILogger<LinkedInHttpClient> logger,
        ICountryDomainProvider countryProvider)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(countryProvider);
        _httpClient = httpClient;
        _options = options?.Value ?? new LinkedInOptions();
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<LinkedInHttpClient>.Instance;
        _countryProvider = countryProvider;

        // Configure default headers to mimic a real browser
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9");
        _httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        _httpClient.DefaultRequestHeaders.Add("DNT", "1");
        _httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
        _httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
    }

    /// <summary>
    /// Search for jobs using the LinkedIn guest API via HTTP.
    /// </summary>
    public async Task<IReadOnlyList<JobListing>> SearchJobsAsync(
        string keywords,
        string location,
        int limit,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(keywords);

        List<JobListing> jobs = [];
        string query = Uri.EscapeDataString(keywords);
        string locationEncoded = Uri.EscapeDataString(location);

        for (int offset = 0; jobs.Count < limit; offset += 25)
        {
            ct.ThrowIfCancellationRequested();

            string baseUrlDomain = _countryProvider.GetDomain(_options.Country);
            string url = $"{baseUrlDomain}/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords={query}&location={locationEncoded}&start={offset}";

            s_logFetching(_logger, url, null);

            try
            {
                HttpResponseMessage response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
                string html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                if (string.IsNullOrEmpty(html))
                {
                    break;
                }

                // Check for rate limiting
                if (html.Contains("429 Too Many Requests", StringComparison.OrdinalIgnoreCase) ||
                    html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                {
                    s_logRateLimited(_logger, url, null);
                    break;
                }

                List<JobListing> foundJobs = ParseJobListingsFromHtml(html, baseUrlDomain);
                if (foundJobs.Count == 0)
                {
                    break;
                }

                foreach (JobListing job in foundJobs)
                {
                    if (jobs.Count >= limit) break;
                    jobs.Add(job);
                }

                if (foundJobs.Count < 25)
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                s_logFetchFailed(_logger, ex);
                break;
            }
        }

        s_logJobsFound(_logger, jobs.Count, null);
        return jobs;
    }

    /// <summary>
    /// Fetch detailed job information using the guest API via HTTP.
    /// </summary>
    public async Task<JobListing?> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(jobId);

        string domain = _countryProvider.GetDomain(_options.Country);
        string url = $"{domain}/jobs-guest/jobs/api/jobPosting/{jobId}";

        s_logFetching(_logger, url, null);

        try
        {
            HttpResponseMessage response = await _httpClient.GetAsync(url, ct).ConfigureAwait(false);
            string html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (string.IsNullOrEmpty(html))
            {
                return null;
            }

            // Check for rate limiting
            if (html.Contains("429", StringComparison.OrdinalIgnoreCase) ||
                html.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
            {
                s_logRateLimited(_logger, url, null);
                return null;
            }

            return ParseJobDetailsFromHtml(html, jobId, url);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            s_logFetchFailed(_logger, ex);
            return null;
        }
    }

    /// <summary>
    /// Parse job listings from the search results HTML.
    /// </summary>
    private List<JobListing> ParseJobListingsFromHtml(string html, string baseUrlDomain)
    {
        List<JobListing> jobs = [];

        // Find all job cards in the HTML
        // The structure is: <li><div class="base-card ..."><a class="base-card__full-link" href="/jobs/view/1234567890/">
        string jobCardPattern = @"<li>\s*<div[^>]*class=""[^""]*base-card[^""]*""[^>]*>.*?<a[^>]*class=""[^""]*base-card__full-link[^""]*""[^>]*href=""(/jobs/view/\d+/?)""";
        MatchCollection matches = Regex.Matches(html, jobCardPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match match in matches)
        {
            try
            {
                Match hrefMatch = Regex.Match(match.Value, @"href=""(/jobs/view/(\d+)/?)""");
                if (!hrefMatch.Success) continue;

                string jobId = hrefMatch.Groups[2].Value;
                string relativeUrl = hrefMatch.Groups[1].Value;
                string fullUrl = $"{baseUrlDomain}{relativeUrl}";

                // Extract title from the sr-only span or other elements
                Match titleMatch = Regex.Match(match.Value, @"<span[^>]*class=""[^""]*sr-only[^""]*""[^>]*>([^<]+)</span>");
                string title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : string.Empty;

                // Try to extract company name from the job card
                Match companyMatch = Regex.Match(match.Value, @"class=""[^""]*base-search-card__subtitle[^""]*""[^>]*>([^<]+)</");
                string company = companyMatch.Success ? companyMatch.Groups[1].Value.Trim() : string.Empty;

                // Try to extract location
                Match locationMatch = Regex.Match(match.Value, @"class=""[^""]*job-search-card__location[^""]*""[^>]*>([^<]+)</");
                string location = locationMatch.Success ? locationMatch.Groups[1].Value.Trim() : string.Empty;

                if (!string.IsNullOrEmpty(jobId))
                {
                    jobs.Add(new JobListing
                    {
                        Id = jobId,
                        Url = fullUrl,
                        Title = title,
                        Company = company,
                        Location = location,
                        Source = "LinkedIn"
                    });
                }
            }
            catch (Exception ex)
            {
                s_logParseJobCardFailed(_logger, ex);
            }
        }

        return jobs;
    }

    /// <summary>
    /// Parse detailed job information from the job details HTML.
    /// </summary>
    private JobListing? ParseJobDetailsFromHtml(string html, string jobId, string url)
    {
        try
        {
            // Try to extract using JSON-LD first
            Match jsonLdMatch = Regex.Match(html, @"<script[^>]*type=""application/ld\+json""[^>]*>(.*?)</script>", RegexOptions.Singleline);
            if (jsonLdMatch.Success)
            {
                string jsonLd = jsonLdMatch.Groups[1].Value;
                // Simple extraction from JSON-LD (in production, use proper JSON parser)
                Match titleMatch = Regex.Match(jsonLd, @"""title""\s*:\s*""([^""]+)""");
                Match companyMatch = Regex.Match(jsonLd, @"""hiringOrganization""\s*:\s*\{[^}]*""name""\s*:\s*""([^""]+)""");
                Match descMatch = Regex.Match(jsonLd, @"""description""\s*:\s*""([^""]+)""");

                if (titleMatch.Success || companyMatch.Success)
                {
                    return new JobListing
                    {
                        Id = jobId,
                        Url = url,
                        Title = titleMatch.Success ? titleMatch.Groups[1].Value : string.Empty,
                        Company = companyMatch.Success ? companyMatch.Groups[1].Value : string.Empty,
                        Description = descMatch.Success ? descMatch.Groups[1].Value : string.Empty,
                        Source = "LinkedIn"
                    };
                }
            }

            // Fallback to DOM scraping using regex
            string[] titleSelectors = new[] {
                @"class=""[^""]*top-card-layout__title[^""]*""[^>]*>([^<]+)</",
                @"<h1[^>]*>([^<]+)</h1>"
            };

            string title = string.Empty;
            foreach (string? selector in titleSelectors)
            {
                Match m = Regex.Match(html, selector);
                if (m.Success)
                {
                    title = m.Groups[1].Value.Trim();
                    break;
                }
            }

            string[] companySelectors = new[] {
                @"class=""[^""]*topcard__org-name-link[^""]*""[^>]*>([^<]+)</",
                @"class=""[^""]*top-card-layout__first-subline[^""]*""[^>]*>.*?<a[^>]*>([^<]+)</"
            };

            string company = string.Empty;
            foreach (string? selector in companySelectors)
            {
                Match m = Regex.Match(html, selector);
                if (m.Success)
                {
                    company = m.Groups[1].Value.Trim();
                    break;
                }
            }

            string[] locationSelectors = new[] {
                @"class=""[^""]*topcard__flavor--bullet[^""]*""[^>]*>([^<]+)</",
                @"class=""[^""]*job-search-card__location[^""]*""[^>]*>([^<]+)</"
            };

            string location = string.Empty;
            foreach (string? selector in locationSelectors)
            {
                Match m = Regex.Match(html, selector);
                if (m.Success)
                {
                    location = m.Groups[1].Value.Trim();
                    break;
                }
            }

            string[] descSelectors = new[] {
                @"class=""[^""]*show-more-less-html__markup[^""]*""[^>]*>(.*?)</div>",
                @"class=""[^""]*description__text[^""]*""[^>]*>(.*?)</div>"
            };

            string description = string.Empty;
            foreach (string? selector in descSelectors)
            {
                Match m = Regex.Match(html, selector, RegexOptions.Singleline);
                if (m.Success)
                {
                    description = m.Groups[1].Value;
                    // Clean up HTML tags
                    description = Regex.Replace(description, @"<[^>]+>", " ");
                    description = Regex.Replace(description, @"\s+", " ").Trim();
                    break;
                }
            }

            return new JobListing
            {
                Id = jobId,
                Url = url,
                Title = title,
                Company = company,
                Location = location,
                Description = description,
                Source = "LinkedIn"
            };
        }
        catch (Exception ex)
        {
            s_logParseJobDetailsFailed(_logger, ex);
            return null;
        }
    }
}
