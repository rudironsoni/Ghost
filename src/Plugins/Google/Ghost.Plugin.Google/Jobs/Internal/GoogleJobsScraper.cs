using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Plugin.Google.Jobs.Entities;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.Google.Jobs.Internal;

/// <summary>
/// Direct HTML scraper for Google Jobs - NO API KEY NEEDED
/// </summary>
public class GoogleJobsScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleJobsScraper> _logger;

    private static readonly Action<ILogger, string, Exception?> s_logScrapingStarted =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1, nameof(ScrapeJobsAsync)), "Scraping Google Jobs from: {Url}");

    private static readonly Action<ILogger, int, Exception?> s_logHtmlReceived =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(2, nameof(ScrapeJobsAsync)), "Received {Length} bytes of HTML");

    private static readonly Action<ILogger, Exception?> s_logJsonLdParsingFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(ScrapeJobsAsync)), "Failed to parse JSON-LD job");

    private static readonly Action<ILogger, int, Exception?> s_logScrapingSuccessful =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(4, nameof(ScrapeJobsAsync)), "Successfully scraped {Count} jobs from Google");

    private static readonly Action<ILogger, Exception?> s_logScrapingFailed =
        LoggerMessage.Define(LogLevel.Error, new EventId(5, nameof(ScrapeJobsAsync)), "Failed to scrape Google Jobs");

    public GoogleJobsScraper(HttpClient httpClient, ILogger<GoogleJobsScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<GoogleJobsEntity>> ScrapeJobsAsync(string query, string location, int maxResults, CancellationToken ct)
    {
        List<GoogleJobsEntity> jobs = [];

        try
        {
            // Build Google search URL for jobs
            string searchQuery = Uri.EscapeDataString($"{query} jobs {location}".Trim());
            string url = $"https://www.google.com/search?q={searchQuery}&ibp=htl;jobs";

            s_logScrapingStarted(_logger, url, null);

            // Set headers to mimic real browser
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Referer", "https://www.google.com/");

            HttpResponseMessage response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);
            string html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            s_logHtmlReceived(_logger, html.Length, null);

            // Parse HTML
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try to find job data in JSON-LD scripts (most reliable)
            HtmlNodeCollection jsonLdScripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (jsonLdScripts != null)
            {
                foreach (HtmlNode? script in jsonLdScripts.Take(maxResults))
                {
                    try
                    {
                        string json = script.InnerText;
                        if (json.Contains("JobPosting"))
                        {
                            GoogleJobsEntity? job = ParseJsonLdJob(json);
                            if (job != null)
                            {
                                jobs.Add(job);
                                if (jobs.Count >= maxResults) break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        s_logJsonLdParsingFailed(_logger, ex);
                    }
                }
            }

            // Fallback: Extract from HTML structure
            if (jobs.Count == 0)
            {
                jobs = ExtractJobsFromHtml(doc, maxResults);
            }

            s_logScrapingSuccessful(_logger, jobs.Count, null);
            return jobs;
        }
        catch (Exception ex)
        {
            s_logScrapingFailed(_logger, ex);
            return jobs;
        }
    }

    private static GoogleJobsEntity? ParseJsonLdJob(string json)
    {
        try
        {
            // Simple JSON parsing for JobPosting schema
            string? title = ExtractJsonValue(json, "\"title\":");
            string? company = ExtractJsonValue(json, "\"name\":");
            string? description = ExtractJsonValue(json, "\"description\":");
            string? location = ExtractJsonValue(json, "\"addressLocality\":");

            if (!string.IsNullOrEmpty(title))
            {
                return new GoogleJobsEntity
                {
                    Title = title,
                    Company = company ?? "Unknown",
                    Description = description ?? "",
                    Location = location ?? ""
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to parse JSON-LD job: {ex.Message}");
            return null;
        }
    }

    private static string? ExtractJsonValue(string json, string key)
    {
        int idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        int start = idx + key.Length;
        while (start < json.Length && (json[start] == '"' || json[start] == ' ' || json[start] == ':'))
            start++;

        if (start >= json.Length) return null;

        int end = json.IndexOf('\"', start);
        if (end < 0) return null;

        return json[start..end].Replace("\\n", " ").Replace("\\", "");
    }

    private static List<GoogleJobsEntity> ExtractJobsFromHtml(HtmlDocument doc, int maxResults)
    {
        List<GoogleJobsEntity> jobs = [];

        // Multiple selector strategies for Google's changing layout
        string[] selectors = new[]
        {
            "//div[@data-ved]//div[contains(@class, 'job')]",
            "//div[contains(@class, 'g')]//div[contains(@data-ved, 'job')]",
            "//div[contains(@class, 'dbsr')]",
            "//div[contains(@class, 'result')]",
            "//div[@data-attribute-name='Job']",
            "//a[contains(@href, 'apply')]//ancestor::div[contains(@class, 'g')]",
            "//div[contains(@class, 'job-listing')]",
            "//div[contains(@class, 'job-title')]//ancestor::div[contains(@class, 'job')]"
        };

        foreach (string? selector in selectors)
        {
            HtmlNodeCollection nodes = doc.DocumentNode.SelectNodes(selector);
            if (nodes != null)
            {
                foreach (HtmlNode? node in nodes.Take(maxResults))
                {
                    try
                    {
                        GoogleJobsEntity? job = ExtractJobFromNode(node);
                        if (job != null && !string.IsNullOrEmpty(job.Title))
                        {
                            jobs.Add(job);
                            if (jobs.Count >= maxResults) break;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to extract job from HTML node: {ex.Message}");
                    }
                }

                if (jobs.Count > 0) break;
            }
        }

        return jobs;
    }

    private static GoogleJobsEntity? ExtractJobFromNode(HtmlNode node)
    {
        try
        {
            string? title = node.SelectSingleNode(".//h3|.//a|.//div[contains(@class, 'title')]|.//div[contains(@class, 'job-title')]")?.InnerText?.Trim();
            string? company = node.SelectSingleNode(".//div[contains(@class, 'company')]|.//span[contains(@class, 'company')]|.//div[contains(@class, 'employer')]")?.InnerText?.Trim();
            string? location = node.SelectSingleNode(".//div[contains(@class, 'location')]|.//span[contains(@class, 'location')]|.//div[contains(@class, 'city')]")?.InnerText?.Trim();
            string? description = node.SelectSingleNode(".//div[contains(@class, 'description')]|.//div[contains(@class, 'summary')]|.//span[contains(@class, 'snippet')]")?.InnerText?.Trim();

            if (!string.IsNullOrEmpty(title))
            {
                return new GoogleJobsEntity
                {
                    Title = title,
                    Company = company ?? "Unknown",
                    Location = location ?? "",
                    Description = description ?? ""
                };
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to extract job from node: {ex.Message}");
            return null;
        }

        return null;
    }
}
