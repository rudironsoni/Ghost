#!/usr/bin/env dotnet-script
#r "nuget: Microsoft.Extensions.Logging.Console, 9.0.0"
#r "nuget: Microsoft.Extensions.Http, 9.0.0"
#r "nuget: HtmlAgilityPack, 1.11.46"

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

// Entity class
public class GoogleJobsEntity
{
    public string Title { get; set; } = "";
    public string Company { get; set; } = "";
    public string Location { get; set; } = "";
    public string Description { get; set; } = "";
    public string Url { get; set; } = "";
    public string Source => "Google";
}

// Scraper class
public class GoogleJobsScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleJobsScraper> _logger;

    public GoogleJobsScraper(HttpClient httpClient, ILogger<GoogleJobsScraper> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<GoogleJobsEntity>> ScrapeJobsAsync(string query, string location, int maxResults, CancellationToken ct)
    {
        var jobs = new List<GoogleJobsEntity>();
        
        try
        {
            // Build Google search URL for jobs
            var searchQuery = Uri.EscapeDataString($"{query} jobs {location}".Trim());
            var url = $"https://www.google.com/search?q={searchQuery}&ibp=htl;jobs";
            
            _logger.LogInformation("Scraping Google Jobs from: {Url}", url);
            
            // Set headers to mimic real browser
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            request.Headers.Add("Accept-Language", "en-US,en;q=0.9");
            request.Headers.Add("Accept-Encoding", "gzip, deflate, br");
            request.Headers.Add("Referer", "https://www.google.com/");
            
            var response = await _httpClient.SendAsync(request, ct);
            var html = await response.Content.ReadAsStringAsync(ct);
            
            _logger.LogInformation("Received {Length} bytes of HTML", html.Length);
            
            // Save HTML for debugging
            await File.WriteAllTextAsync("/tmp/google-jobs-response.html", html);
            Console.WriteLine($"HTML saved to /tmp/google-jobs-response.html");
            
            // Parse HTML
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            
            // Try to find job data in JSON-LD scripts (most reliable)
            var jsonLdScripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
            if (jsonLdScripts != null)
            {
                Console.WriteLine($"Found {jsonLdScripts.Count} JSON-LD scripts");
                foreach (var script in jsonLdScripts.Take(maxResults))
                {
                    try
                    {
                        var json = script.InnerText;
                        if (json.Contains("JobPosting"))
                        {
                            var job = ParseJsonLdJob(json);
                            if (job != null)
                            {
                                jobs.Add(job);
                                if (jobs.Count >= maxResults) break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse JSON-LD job");
                    }
                }
            }
            
            // Fallback: Extract from HTML structure
            if (jobs.Count == 0)
            {
                Console.WriteLine("No JSON-LD jobs found, trying HTML extraction...");
                jobs = ExtractJobsFromHtml(doc, maxResults);
            }
            
            _logger.LogInformation("Successfully scraped {Count} jobs from Google", jobs.Count);
            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Google Jobs");
            return jobs;
        }
    }
    
    private GoogleJobsEntity? ParseJsonLdJob(string json)
    {
        try
        {
            // Simple JSON parsing for JobPosting schema
            var title = ExtractJsonValue(json, "\"title\":");
            var company = ExtractJsonValue(json, "\"name\":");
            var description = ExtractJsonValue(json, "\"description\":");
            var location = ExtractJsonValue(json, "\"addressLocality\":");
            
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
        catch { }
        
        return null;
    }
    
    private string? ExtractJsonValue(string json, string key)
    {
        var idx = json.IndexOf(key, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        
        var start = idx + key.Length;
        while (start < json.Length && (json[start] == '"' || json[start] == ' ' || json[start] == ':'))
            start++;
        
        if (start >= json.Length) return null;
        
        var end = json.IndexOf("\"", start);
        if (end < 0) return null;
        
        return json[start..end].Replace("\\n", " ").Replace("\\", "");
    }
    
    private List<GoogleJobsEntity> ExtractJobsFromHtml(HtmlDocument doc, int maxResults)
    {
        var jobs = new List<GoogleJobsEntity>();
        
        // Multiple selector strategies for Google's changing layout
        var selectors = new[]
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
        
        foreach (var selector in selectors)
        {
            var nodes = doc.DocumentNode.SelectNodes(selector);
            if (nodes != null)
            {
                Console.WriteLine($"Selector '{selector}' found {nodes.Count} nodes");
                foreach (var node in nodes.Take(maxResults))
                {
                    try
                    {
                        var job = ExtractJobFromNode(node);
                        if (job != null && !string.IsNullOrEmpty(job.Title))
                        {
                            jobs.Add(job);
                            if (jobs.Count >= maxResults) break;
                        }
                    }
                    catch { }
                }
                
                if (jobs.Count > 0) break;
            }
            else
            {
                Console.WriteLine($"Selector '{selector}' found no nodes");
            }
        }
        
        return jobs;
    }
    
    private GoogleJobsEntity? ExtractJobFromNode(HtmlNode node)
    {
        try
        {
            var title = node.SelectSingleNode(".//h3|.//a|.//div[contains(@class, 'title')]|.//div[contains(@class, 'job-title')]")?.InnerText?.Trim();
            var company = node.SelectSingleNode(".//div[contains(@class, 'company')]|.//span[contains(@class, 'company')]|.//div[contains(@class, 'employer')]")?.InnerText?.Trim();
            var location = node.SelectSingleNode(".//div[contains(@class, 'location')]|.//span[contains(@class, 'location')]|.//div[contains(@class, 'city')]")?.InnerText?.Trim();
            var description = node.SelectSingleNode(".//div[contains(@class, 'description')]|.//div[contains(@class, 'summary')]|.//span[contains(@class, 'snippet')]")?.InnerText?.Trim();
            
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
        catch { }
        
        return null;
    }
}

// Main execution
var services = new ServiceCollection();
services.AddLogging(builder => 
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
services.AddHttpClient();

var provider = services.BuildServiceProvider();
var logger = provider.GetRequiredService<ILogger<GoogleJobsScraper>>();
var httpClient = provider.GetRequiredService<HttpClient>();

var scraper = new GoogleJobsScraper(httpClient, logger);

Console.WriteLine("Starting live Google Jobs scrape...");
Console.WriteLine("Query: software engineer");
Console.WriteLine("Location: (any)");
Console.WriteLine("Max Results: 5");
Console.WriteLine();

var jobs = await scraper.ScrapeJobsAsync("software engineer", "", 5, CancellationToken.None);

Console.WriteLine();
Console.WriteLine("=== RESULTS ===");
Console.WriteLine($"Found {jobs.Count} jobs");
Console.WriteLine();

for (int i = 0; i < jobs.Count; i++)
{
    var job = jobs[i];
    Console.WriteLine($"Job {i + 1}:");
    Console.WriteLine($"  Title: {job.Title}");
    Console.WriteLine($"  Company: {job.Company}");
    Console.WriteLine($"  Location: {job.Location}");
    Console.WriteLine($"  URL: {job.Url}");
    Console.WriteLine($"  Source: {job.Source}");
    Console.WriteLine($"  Description: {(job.Description.Length > 100 ? job.Description.Substring(0, 100) + "..." : job.Description)}");
    Console.WriteLine();
}

if (jobs.Count == 0)
{
    Console.WriteLine("WARNING: No jobs extracted. Check /tmp/google-jobs-response.html for debugging.");
}
