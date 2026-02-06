// LinkedIn HTML Fixture Scraper
// Captures real LinkedIn job search results and job detail pages for test fixtures

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Ghost.Platform.LinkedIn.Tests.Fixtures;

public class LinkedInFixtureScraper
{
    private const string SearchQuery = "software engineer";
    private const string SearchLocation = "";
    private const string FixturesDir = ".";

#pragma warning disable CS8892 // Method will not be used as an entry point
    public static async Task Main()
#pragma warning restore CS8892
    {
        Console.WriteLine("=== LinkedIn Fixture Scraper ===");
        Console.WriteLine($"Search Query: {SearchQuery}");
        Console.WriteLine($"Output Directory: {Path.GetFullPath(FixturesDir)}");
        Console.WriteLine();

        using var playwright = await Playwright.CreateAsync();

        var launchOptions = new BrowserTypeLaunchOptions
        {
            Headless = true,
            Args = new[] { "--disable-blink-features=AutomationControlled" }
        };

        await using var browser = await playwright.Chromium.LaunchAsync(launchOptions);
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            Locale = "en-US",
            TimezoneId = "America/New_York"
        });

        var page = await context.NewPageAsync();

        try
        {
            // Step 1: Scrape search results
            Console.WriteLine("[1/6] Scraping LinkedIn job search results...");
            var jobIds = await ScrapeSearchResultsAsync(page);

            if (jobIds.Count == 0)
            {
                Console.WriteLine("ERROR: No job IDs found in search results. LinkedIn may be blocking requests.");
                return;
            }

            Console.WriteLine($"Found {jobIds.Count} job IDs");

            // Step 2: Scrape individual job details
            var jobsToScrape = Math.Min(5, jobIds.Count);
            Console.WriteLine($"[2/6] Scraping {jobsToScrape} individual job detail pages...");

            for (int i = 0; i < jobsToScrape; i++)
            {
                Console.WriteLine($"  Scraping job {i + 1}/{jobsToScrape} (ID: {jobIds[i]})...");
                await ScrapeJobDetailsAsync(page, jobIds[i], i + 1);
                await Task.Delay(2000);
            }

            Console.WriteLine();
            Console.WriteLine("=== Scraping Complete ===");
            Console.WriteLine($"Files saved to: {Path.GetFullPath(FixturesDir)}");
            ListFixtures();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            await context.CloseAsync();
        }
    }

    private static async Task<List<string>> ScrapeSearchResultsAsync(Microsoft.Playwright.IPage page)
    {
        var encodedQuery = Uri.EscapeDataString(SearchQuery);
        var encodedLocation = Uri.EscapeDataString(SearchLocation);
        var searchUrl = $"https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search?keywords={encodedQuery}&location={encodedLocation}&start=0";

        Console.WriteLine($"  Navigating to: {searchUrl}");

        await page.GotoAsync(searchUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        await Task.Delay(3000);

        var html = await page.ContentAsync();

        var searchResultsPath = Path.Combine(FixturesDir, "linkedin-search-results.html");
        await File.WriteAllTextAsync(searchResultsPath, html);
        Console.WriteLine($"  Saved: {searchResultsPath}");

        var jobIds = ExtractJobIdsFromSearchHtml(html);
        return jobIds;
    }

    private static List<string> ExtractJobIdsFromSearchHtml(string html)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>();

        var pattern1 = new Regex(@"data-entity-urn=""urn:li:jobPosting:(?<id>\d+)""", RegexOptions.IgnoreCase);
        foreach (Match m in pattern1.Matches(html))
        {
            var id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && seen.Add(id))
                ids.Add(id);
        }

        var pattern2 = new Regex(@"/jobs/(?:view|r)/(?<id>\d+)", RegexOptions.IgnoreCase);
        foreach (Match m in pattern2.Matches(html))
        {
            var id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && seen.Add(id))
                ids.Add(id);
        }

        var pattern3 = new Regex(@"[?&](?:jobId|id)=(?<id>\d+)", RegexOptions.IgnoreCase);
        foreach (Match m in pattern3.Matches(html))
        {
            var id = m.Groups["id"].Value;
            if (!string.IsNullOrEmpty(id) && seen.Add(id))
                ids.Add(id);
        }

        return ids;
    }

    private static async Task ScrapeJobDetailsAsync(Microsoft.Playwright.IPage page, string jobId, int index)
    {
        var jobUrl = $"https://www.linkedin.com/jobs-guest/jobs/api/jobPosting/{jobId}";

        Console.WriteLine($"  Navigating to: {jobUrl}");

        await page.GotoAsync(jobUrl, new PageGotoOptions
        {
            WaitUntil = WaitUntilState.NetworkIdle,
            Timeout = 30000
        });

        await Task.Delay(2000);

        var html = await page.ContentAsync();

        var jobDetailsPath = Path.Combine(FixturesDir, $"linkedin-job-detail-{index}.html");
        await File.WriteAllTextAsync(jobDetailsPath, html);
        Console.WriteLine($"  Saved: {jobDetailsPath}");

        var title = await ExtractTextAsync(page, ".top-card-layout__title, h1");
        var company = await ExtractTextAsync(page, ".top-card-layout__first-subline .topcard__org-name-link");
        var location = await ExtractTextAsync(page, ".top-card-layout__first-subline .topcard__flavor--bullet");

        Console.WriteLine($"    Title: {title ?? "N/A"}");
        Console.WriteLine($"    Company: {company ?? "N/A"}");
        Console.WriteLine($"    Location: {location ?? "N/A"}");
    }

    private static async Task<string?> ExtractTextAsync(Microsoft.Playwright.IPage page, string selectors)
    {
        try
        {
            var selectorArray = selectors.Split(',').Select(s => s.Trim()).ToArray();
            foreach (var selector in selectorArray)
            {
                var element = await page.QuerySelectorAsync(selector);
                if (element != null)
                {
                    var text = await element.TextContentAsync();
                    if (!string.IsNullOrWhiteSpace(text))
                        return text.Trim();
                }
            }
        }
        catch { }
        return null;
    }

    private static void ListFixtures()
    {
        Console.WriteLine();
        Console.WriteLine("Generated Files:");
        var files = Directory.GetFiles(FixturesDir, "*.html").OrderBy(f => f);
        foreach (var file in files)
        {
            var info = new FileInfo(file);
            Console.WriteLine($"  - {Path.GetFileName(file)} ({info.Length / 1024} KB)");
        }
    }
}
