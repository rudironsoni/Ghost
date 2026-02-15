// RealScrapingVerification.cs
// Direct HTTP scraping test for Ghost job plugins
// Tests: Google Jobs, Indeed, Glassdoor, LinkedIn

using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using System.Web;
using Ghost.Contracts.Jobs;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

Console.WriteLine("=== Ghost Real Scraping Verification Test ===");
Console.WriteLine("Testing job scrapers with REAL data from job websites\n");

// Test parameters
var query = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault() ?? "software engineer";
var location = Environment.GetCommandLineArgs().Skip(2).FirstOrDefault() ?? "United States";

Console.WriteLine($"Search Query: '{query}'");
Console.WriteLine($"Location: '{location}'\n");

// Configure HTTP client with automatic decompression
var handler = new HttpClientHandler
{
    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,
    AllowAutoRedirect = true,
    UseCookies = true
};
var httpClient = new HttpClient(handler);
httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
httpClient.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en-US,en;q=0.9");
httpClient.DefaultRequestHeaders.Referrer = new Uri("https://www.google.com/");
httpClient.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
httpClient.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
httpClient.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
httpClient.Timeout = TimeSpan.FromSeconds(30);

// Create logger
var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());

// Track results
var allJobs = new List<JobListing>();
var successCount = 0;

// Test 1: Google Jobs
Console.WriteLine("--- Testing Google Jobs ---");
try
{
    var jobs = await TestGoogleJobs(httpClient, loggerFactory, query, location);
    allJobs.AddRange(jobs);
    successCount += jobs.Count > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ Google Jobs failed: {ex.Message}\n");
}

// Test 2: Indeed
Console.WriteLine("--- Testing Indeed ---");
try
{
    var jobs = await TestIndeed(httpClient, query, location);
    allJobs.AddRange(jobs);
    successCount += jobs.Count > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ Indeed failed: {ex.Message}\n");
}

// Test 3: Glassdoor (browser-only - show info)
Console.WriteLine("--- Testing Glassdoor ---");
Console.WriteLine("  ℹ Glassdoor requires browser automation for complete test");
Console.WriteLine("  Using direct HTTP fallback...\n");
try
{
    var jobs = await TestGlassdoor(httpClient, query, location);
    allJobs.AddRange(jobs);
    successCount += jobs.Count > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ Glassdoor failed: {ex.Message}\n");
}

// Test 4: LinkedIn (browser-only - show info)
Console.WriteLine("--- Testing LinkedIn ---");
Console.WriteLine("  ℹ LinkedIn requires browser automation for complete test");
Console.WriteLine("  Using direct HTTP fallback...\n");
try
{
    var jobs = await TestLinkedIn(httpClient, query, location);
    allJobs.AddRange(jobs);
    successCount += jobs.Count > 0 ? 1 : 0;
}
catch (Exception ex)
{
    Console.WriteLine($"  ❌ LinkedIn failed: {ex.Message}\n");
}

// Summary
Console.WriteLine("=== Test Summary ===");
Console.WriteLine($"Total jobs found: {allJobs.Count}");
Console.WriteLine($"Platforms with results: {successCount}/4");

if (allJobs.Count > 0)
{
    Console.WriteLine("\n✅ SUCCESS: At least one scraper returned results");

    // Show sample jobs
    Console.WriteLine("\n=== Sample Jobs ===");
    foreach (var job in allJobs.Take(10))
    {
        Console.WriteLine($"\n📋 {job.Title}");
        Console.WriteLine($"   Company: {job.Company}");
        Console.WriteLine($"   Location: {job.Location}");
        Console.WriteLine($"   Source: {job.Source}");
        if (!string.IsNullOrEmpty(job.Url))
            Console.WriteLine($"   URL: {job.Url.Substring(0, Math.Min(60, job.Url.Length))}...");
    }
}
else
{
    Console.WriteLine("\n❌ FAILURE: No jobs found from any source");
    Console.WriteLine("\nThis indicates the scrapers are not working correctly.");
    Console.WriteLine("Check: HTML structure changes, anti-bot protection, or network issues.");
    Environment.Exit(1);
}

Console.WriteLine("\n=== Test Complete ===");

// Helper functions
static async Task<List<JobListing>> TestGoogleJobs(HttpClient httpClient, ILoggerFactory loggerFactory, string query, string location)
{
    var url = $"https://www.google.com/search?q={Uri.EscapeDataString(query)}+jobs+in+{Uri.EscapeDataString(location)}&ibp=htl;jobs&start=0";
    Console.WriteLine($"  Fetching: {url}");

    var html = await httpClient.GetStringAsync(url);
    Console.WriteLine($"  Received {html.Length} bytes");

    // Save for debugging
    var htmlPath = Path.Combine(Path.GetTempPath(), "google_jobs_response.html");
    await File.WriteAllTextAsync(htmlPath, html);
    Console.WriteLine($"  Saved HTML to: {htmlPath}");

    var jobs = ParseGoogleJobs(html, location);
    Console.WriteLine($"  ✓ Found {jobs.Count} jobs");

    if (jobs.Count > 0)
    {
        Console.WriteLine("\n  Sample results:");
        foreach (var job in jobs.Take(3))
        {
            Console.WriteLine($"    - {job.Title} @ {job.Company}");
        }
    }
    Console.WriteLine();
    return jobs;
}

static async Task<List<JobListing>> TestIndeed(HttpClient httpClient, string query, string location)
{
    var url = $"https://www.indeed.com/jobs?q={Uri.EscapeDataString(query)}&l={Uri.EscapeDataString(location)}";
    Console.WriteLine($"  Fetching: {url}");

    var html = await httpClient.GetStringAsync(url);
    Console.WriteLine($"  Received {html.Length} bytes");

    var htmlPath = Path.Combine(Path.GetTempPath(), "indeed_response.html");
    await File.WriteAllTextAsync(htmlPath, html);
    Console.WriteLine($"  Saved HTML to: {htmlPath}");

    var jobs = ParseIndeed(html);
    Console.WriteLine($"  ✓ Found {jobs.Count} jobs");

    if (jobs.Count > 0)
    {
        Console.WriteLine("\n  Sample results:");
        foreach (var job in jobs.Take(3))
        {
            Console.WriteLine($"    - {job.Title} @ {job.Company}");
        }
    }
    Console.WriteLine();
    return jobs;
}

static async Task<List<JobListing>> TestGlassdoor(HttpClient httpClient, string query, string location)
{
    var url = $"https://www.glassdoor.com/Job/jobs.htm?suggestCount=0&suggestChosen=false&clickSource=searchBtn&typedKeyword={Uri.EscapeDataString(query)}&sc.keyword={Uri.EscapeDataString(query)}&locT=N&locId=1&jobType=all";
    Console.WriteLine($"  Fetching: {url}");

    var html = await httpClient.GetStringAsync(url);
    Console.WriteLine($"  Received {html.Length} bytes");

    var htmlPath = Path.Combine(Path.GetTempPath(), "glassdoor_response.html");
    await File.WriteAllTextAsync(htmlPath, html);
    Console.WriteLine($"  Saved HTML to: {htmlPath}");

    var jobs = ParseGlassdoor(html);
    Console.WriteLine($"  ✓ Found {jobs.Count} jobs");
    Console.WriteLine();
    return jobs;
}

static async Task<List<JobListing>> TestLinkedIn(HttpClient httpClient, string query, string location)
{
    var url = $"https://www.linkedin.com/jobs/search?keywords={Uri.EscapeDataString(query)}&location={Uri.EscapeDataString(location)}";
    Console.WriteLine($"  Fetching: {url}");

    var html = await httpClient.GetStringAsync(url);
    Console.WriteLine($"  Received {html.Length} bytes");

    var htmlPath = Path.Combine(Path.GetTempPath(), "linkedin_response.html");
    await File.WriteAllTextAsync(htmlPath, html);
    Console.WriteLine($"  Saved HTML to: {htmlPath}");

    var jobs = ParseLinkedIn(html);
    Console.WriteLine($"  ✓ Found {jobs.Count} jobs");
    Console.WriteLine();
    return jobs;
}

static List<JobListing> ParseGoogleJobs(string html, string location)
{
    var jobs = new List<JobListing>();

    // Strategy 1: Look for JSON-LD job postings
    var jsonLdPattern = @"<script type=""application/ld\+json"">(.*?)</script>";
    var jsonLdMatches = Regex.Matches(html, jsonLdPattern, RegexOptions.Singleline);

    foreach (Match match in jsonLdMatches)
    {
        var json = match.Groups[1].Value;
        if (json.Contains("JobPosting"))
        {
            try
            {
                var title = ExtractJsonValue(json, "title");
                var company = ExtractJsonValue(json, "hiringOrganization");
                var jobLoc = ExtractJsonValue(json, "jobLocation");
                var description = ExtractJsonValue(json, "description");
                var url = ExtractJsonValue(json, "url");

                if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(company))
                {
                    jobs.Add(new JobListing
                    {
                        Id = Guid.NewGuid().ToString(),
                        Title = title,
                        Company = company,
                        Location = jobLoc ?? location ?? "",
                        Description = description ?? "",
                        Url = url ?? "",
                        Source = "Google Jobs",
                        PostedAt = DateTime.UtcNow
                    });
                }
            }
            catch { /* Skip malformed JSON */ }
        }
    }

    // Strategy 2: Parse HTML structure
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    // Look for job cards
    var jobCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'job-card')] | //div[contains(@class, 'gws-plugins-horizon-jobs__li')]");
    if (jobCards != null)
    {
        foreach (var card in jobCards)
        {
            var title = card.SelectSingleNode(".//h3 | .//h2 | .//div[contains(@class, 'title')]")?.InnerText?.Trim();
            var company = card.SelectSingleNode(".//div[contains(@class, 'company')] | .//span[contains(@class, 'company')]")?.InnerText?.Trim();

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(company))
            {
                jobs.Add(new JobListing
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Company = company,
                    Location = location ?? "",
                    Source = "Google Jobs",
                    PostedAt = DateTime.UtcNow
                });
            }
        }
    }

    return jobs.DistinctBy(j => j.Title + j.Company).ToList();
}

static List<JobListing> ParseIndeed(string html)
{
    var jobs = new List<JobListing>();
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    // Strategy 1: JSON-LD
    var scripts = doc.DocumentNode.SelectNodes("//script[@type='application/ld+json']");
    if (scripts != null)
    {
        foreach (var script in scripts)
        {
            var json = script.InnerText;
            if (json.Contains("JobPosting"))
            {
                try
                {
                    var title = ExtractJsonValue(json, "title");
                    var company = ExtractJsonValue(json, "name");
                    var jobLoc = ExtractJsonValue(json, "addressLocality");
                    var url = ExtractJsonValue(json, "url");

                    if (!string.IsNullOrEmpty(title))
                    {
                        jobs.Add(new JobListing
                        {
                            Id = Guid.NewGuid().ToString(),
                            Title = title,
                            Company = company ?? "Unknown",
                            Location = jobLoc ?? "",
                            Url = url ?? "",
                            Source = "Indeed",
                            PostedAt = DateTime.UtcNow
                        });
                    }
                }
                catch { }
            }
        }
    }

    // Strategy 2: HTML parsing
    var jobCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'job_seen_beacon')] | //div[contains(@class, 'slider_container')] | //table[@role='presentation']//td[@role='presentation']");
    if (jobCards != null)
    {
        foreach (var card in jobCards)
        {
            var titleNode = card.SelectSingleNode(".//h2//a | .//a[contains(@class, 'jcs-JobTitle')] | .//h2[contains(@class, 'jobTitle')]//a");
            var companyNode = card.SelectSingleNode(".//span[contains(@class, 'companyName')] | .//span[contains(@data-testid, 'company-name')]");
            var locationNode = card.SelectSingleNode(".//div[contains(@class, 'companyLocation')] | .//div[contains(@data-testid, 'text-location')]");

            var title = titleNode?.InnerText?.Trim();
            var company = companyNode?.InnerText?.Trim();
            var loc = locationNode?.InnerText?.Trim();

            if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(company))
            {
                jobs.Add(new JobListing
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Company = company,
                    Location = loc ?? "",
                    Url = titleNode?.GetAttributeValue("href", "") ?? "",
                    Source = "Indeed",
                    PostedAt = DateTime.UtcNow
                });
            }
        }
    }

    return jobs.DistinctBy(j => j.Title + j.Company).ToList();
}

static List<JobListing> ParseGlassdoor(string html)
{
    var jobs = new List<JobListing>();
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    // Parse Glassdoor job cards
    var jobCards = doc.DocumentNode.SelectNodes("//li[contains(@class, 'react-job-listing')] | //div[contains(@class, 'jobContainer')]");
    if (jobCards != null)
    {
        foreach (var card in jobCards)
        {
            var titleNode = card.SelectSingleNode(".//a[contains(@class, 'job-title')] | .//h2//a");
            var companyNode = card.SelectSingleNode(".//div[contains(@class, 'employerName')] | .//a[contains(@class, 'company')]");
            var locationNode = card.SelectSingleNode(".//span[contains(@class, 'loc')] | .//div[contains(@class, 'location')]");

            var title = titleNode?.InnerText?.Trim();
            var company = companyNode?.InnerText?.Trim();
            var loc = locationNode?.InnerText?.Trim();

            if (!string.IsNullOrEmpty(title))
            {
                jobs.Add(new JobListing
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Company = company ?? "Unknown",
                    Location = loc ?? "",
                    Url = titleNode?.GetAttributeValue("href", "") ?? "",
                    Source = "Glassdoor",
                    PostedAt = DateTime.UtcNow
                });
            }
        }
    }

    return jobs.DistinctBy(j => j.Title + j.Company).ToList();
}

static List<JobListing> ParseLinkedIn(string html)
{
    var jobs = new List<JobListing>();
    var doc = new HtmlDocument();
    doc.LoadHtml(html);

    // Parse LinkedIn job cards
    var jobCards = doc.DocumentNode.SelectNodes("//div[contains(@class, 'job-search-card')] | //div[contains(@class, 'jobs-search__job-card')] | //li[contains(@class, 'jobs-search-results__list-item')]");
    if (jobCards != null)
    {
        foreach (var card in jobCards)
        {
            var titleNode = card.SelectSingleNode(".//h3//a | .//a[contains(@class, 'job-card-list__title')]");
            var companyNode = card.SelectSingleNode(".//h4//a | .//span[contains(@class, 'job-card-container__company-name')]");
            var locationNode = card.SelectSingleNode(".//span[contains(@class, 'job-card-container__metadata')]");

            var title = titleNode?.InnerText?.Trim();
            var company = companyNode?.InnerText?.Trim();
            var loc = locationNode?.InnerText?.Trim();

            if (!string.IsNullOrEmpty(title))
            {
                jobs.Add(new JobListing
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = title,
                    Company = company ?? "Unknown",
                    Location = loc ?? "",
                    Url = titleNode?.GetAttributeValue("href", "") ?? "",
                    Source = "LinkedIn",
                    PostedAt = DateTime.UtcNow
                });
            }
        }
    }

    return jobs.DistinctBy(j => j.Title + j.Company).ToList();
}

static string ExtractJsonValue(string json, string key)
{
    var pattern = @"\u0022" + key + @"\u0022\s*:\s*\u0022([^\u0022]+)\u0022|\u0027" + key + @"\u0027\s*:\s*\u0027([^\u0027]+)\u0027";
    var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
    return match.Success ? (match.Groups[1].Value ?? match.Groups[2].Value) : "";
}

// Null proxy provider for DI
internal sealed class NullProxyProvider : Ghost.Abstractions.IProxyProvider
{
    public Task<Ghost.Abstractions.ProxyInfo?> GetProxyAsync(string countryCode, CancellationToken token = default)
        => Task.FromResult<Ghost.Abstractions.ProxyInfo?>(null);
}
