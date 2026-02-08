using System.Text.Json;
using System.Text.RegularExpressions;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed.Internal;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace Ghost.Platform.Indeed.Jobs;

/// <summary>
/// Scraper for extracting detailed job information from Indeed job detail pages.
/// Implements multi-strategy fallback: API primary, browser fallback.
/// </summary>
public sealed class IndeedJobDetailsScraper
{
    private readonly IBrowserContext? _browserContext;
    private readonly IndeedApiClient _apiClient;
    private readonly ILogger<IndeedJobDetailsScraper> _logger;

    private static readonly Action<ILogger, string, Exception?> LogFetchingDetails =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3001, "FetchingJobDetails"), "Fetching job details for ID: {JobId}");

    private static readonly Action<ILogger, string, Exception?> LogApiFetchSuccess =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3002, "ApiFetchSuccess"), "Successfully fetched job details from API for {JobId}");

    private static readonly Action<ILogger, string, Exception?> LogApiFetchFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3003, "ApiFetchFailed"), "API fetch failed for {JobId}, trying browser fallback");

    private static readonly Action<ILogger, string, Exception?> LogBrowserFetchSuccess =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3004, "BrowserFetchSuccess"), "Successfully fetched job details from browser for {JobId}");

    private static readonly Action<ILogger, string, Exception?> LogBrowserFetchFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3005, "BrowserFetchFailed"), "Browser fallback failed for {JobId}");

    public IndeedJobDetailsScraper(
        IndeedApiClient apiClient,
        ILogger<IndeedJobDetailsScraper> logger)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IndeedJobDetailsScraper(
        IBrowserContext browserContext,
        IndeedApiClient apiClient,
        ILogger<IndeedJobDetailsScraper> logger)
    {
        _browserContext = browserContext ?? throw new ArgumentNullException(nameof(browserContext));
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets detailed job information for a specific job ID.
    /// </summary>
    public async Task<JobListing> GetJobDetailsAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        LogFetchingDetails(_logger, jobId, null);

        // Strategy 1: Try API first (if GraphQL endpoint supports job details)
        try
        {
            var jobDetails = await FetchFromApiAsync(jobId, ct);
            if (jobDetails != null)
            {
                LogApiFetchSuccess(_logger, jobId, null);
                return jobDetails;
            }
        }
        catch (Exception ex)
        {
            LogApiFetchFailed(_logger, jobId, ex);
        }

        // Strategy 2: Fallback to browser scraping
        if (_browserContext != null)
        {
            try
            {
                var jobDetails = await FetchFromBrowserAsync(jobId, ct);
                if (jobDetails != null)
                {
                    LogBrowserFetchSuccess(_logger, jobId, null);
                    return jobDetails;
                }
            }
            catch (Exception ex)
            {
                LogBrowserFetchFailed(_logger, jobId, ex);
            }
        }

        // Return minimal job listing if all strategies fail
        return new JobListing
        {
            Id = jobId,
            Source = "Indeed",
            Url = $"https://indeed.com/viewjob?jk={jobId}"
        };
    }

    /// <summary>
    /// Attempts to fetch job details from Indeed API.
    /// </summary>
    private async Task<JobListing?> FetchFromApiAsync(string jobId, CancellationToken ct)
    {
        // Indeed GraphQL API doesn't have a direct job details query in the public schema
        // This would require authentication or a different endpoint
        // For now, return null to force browser fallback
        await Task.CompletedTask;
        return null;
    }

    /// <summary>
    /// Fetches job details by scraping the Indeed job detail page with a browser.
    /// </summary>
    private async Task<JobListing?> FetchFromBrowserAsync(string jobId, CancellationToken ct)
    {
        if (_browserContext == null)
        {
            return null;
        }

        var page = await _browserContext.NewPageAsync();
        try
        {
            var url = $"https://indeed.com/viewjob?jk={jobId}";
            await page.GotoAsync(url, new() { WaitUntil = WaitUntilState.NetworkIdle, Timeout = 30000 });

            // Wait for job content to load
            await page.WaitForSelectorAsync("div[id*='jobDescriptionText'], .jobsearch-jobDescriptionText", new() { Timeout = 10000 });

            var html = await page.ContentAsync();
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Extract structured data from JSON-LD (most reliable)
            var jsonLdData = await ExtractJsonLdDataAsync(page);
            if (jsonLdData != null)
            {
                return jsonLdData;
            }

            // Fallback: Parse HTML structure
            return ParseJobDetailsFromHtml(doc, jobId, url);
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    /// <summary>
    /// Extracts job data from JSON-LD structured data embedded in the page.
    /// </summary>
    private async Task<JobListing?> ExtractJsonLdDataAsync(IPage page)
    {
        try
        {
            var jsonLdScript = await page.Locator("script[type='application/ld+json']").FirstOrDefaultAsync();
            if (jsonLdScript == null)
            {
                return null;
            }

            var jsonContent = await jsonLdScript.InnerTextAsync();
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // Check if it's a JobPosting schema
            if (!root.TryGetProperty("@type", out var typeEl) || typeEl.GetString() != "JobPosting")
            {
                return null;
            }

            var jobId = root.TryGetProperty("identifier", out var idEl) && idEl.TryGetProperty("value", out var valueEl)
                ? valueEl.GetString() ?? string.Empty
                : string.Empty;

            var title = root.TryGetProperty("title", out var titleEl) ? titleEl.GetString() ?? string.Empty : string.Empty;
            var description = root.TryGetProperty("description", out var descEl) ? descEl.GetString() ?? string.Empty : string.Empty;

            var company = root.TryGetProperty("hiringOrganization", out var orgEl) && orgEl.TryGetProperty("name", out var nameEl)
                ? nameEl.GetString() ?? string.Empty
                : string.Empty;

            var location = root.TryGetProperty("jobLocation", out var locEl) && locEl.TryGetProperty("address", out var addrEl)
                ? ExtractLocationFromAddress(addrEl)
                : string.Empty;

            var salary = root.TryGetProperty("baseSalary", out var salaryEl) ? ExtractSalaryFromJsonLd(salaryEl) : string.Empty;

            var url = root.TryGetProperty("url", out var urlEl) ? urlEl.GetString() ?? $"https://indeed.com/viewjob?jk={jobId}" : $"https://indeed.com/viewjob?jk={jobId}";

            var datePosted = root.TryGetProperty("datePosted", out var dateEl) && DateTimeOffset.TryParse(dateEl.GetString(), out var date)
                ? date
                : DateTimeOffset.UtcNow;

            return new JobListing
            {
                Id = jobId,
                Title = title,
                Company = company,
                Location = location,
                Description = HtmlSanitizer.StripHtmlTags(description),
                Salary = salary,
                Url = url,
                PostedAt = datePosted,
                Source = "Indeed"
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses job details from HTML structure (fallback method).
    /// </summary>
    private JobListing ParseJobDetailsFromHtml(HtmlDocument doc, string jobId, string url)
    {
        // Extract title
        var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'jobsearch-JobInfoHeader-title')]");
        var title = titleNode?.InnerText?.Trim() ?? string.Empty;

        // Extract company
        var companyNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'jobsearch-InlineCompanyRating')]//a | //div[contains(@class, 'jobsearch-CompanyInfoContainer')]//a");
        var company = companyNode?.InnerText?.Trim() ?? string.Empty;

        // Extract location
        var locationNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'jobsearch-JobInfoHeader-subtitle')]//div[contains(@class, 'location')]");
        var location = locationNode?.InnerText?.Trim() ?? string.Empty;

        // Extract description (multiple selectors for robustness)
        var descriptionNode = doc.DocumentNode.SelectSingleNode(
            "//div[@id='jobDescriptionText'] | //div[contains(@class, 'jobsearch-jobDescriptionText')]");
        var description = descriptionNode?.InnerText?.Trim() ?? string.Empty;

        // Extract salary
        var salaryNode = doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'jobsearch-JobMetadataHeader-item')]//span[contains(text(), '$')]");
        var salary = salaryNode?.InnerText?.Trim() ?? string.Empty;

        // Extract benefits (if present)
        var benefitsNode = doc.DocumentNode.SelectSingleNode("//div[contains(@id, 'benefits')] | //div[contains(@class, 'benefits')]");
        var benefits = benefitsNode?.InnerText?.Trim() ?? string.Empty;
        if (!string.IsNullOrEmpty(benefits))
        {
            description += $"\n\nBenefits:\n{benefits}";
        }

        return new JobListing
        {
            Id = jobId,
            Title = title,
            Company = company,
            Location = location,
            Description = description,
            Salary = salary,
            Url = url,
            Source = "Indeed"
        };
    }

    private static string ExtractLocationFromAddress(JsonElement address)
    {
        var parts = new List<string>();

        if (address.TryGetProperty("addressLocality", out var locality) && !string.IsNullOrEmpty(locality.GetString()))
        {
            parts.Add(locality.GetString()!);
        }

        if (address.TryGetProperty("addressRegion", out var region) && !string.IsNullOrEmpty(region.GetString()))
        {
            parts.Add(region.GetString()!);
        }

        return string.Join(", ", parts);
    }

    private static string ExtractSalaryFromJsonLd(JsonElement salary)
    {
        if (!salary.TryGetProperty("value", out var valueEl))
        {
            return string.Empty;
        }

        // Handle MonetaryAmount schema
        if (valueEl.TryGetProperty("minValue", out var minEl) && valueEl.TryGetProperty("maxValue", out var maxEl))
        {
            var min = minEl.GetDecimal();
            var max = maxEl.GetDecimal();
            var currency = valueEl.TryGetProperty("currency", out var currEl) ? currEl.GetString() ?? "USD" : "USD";

            return $"${min:N0} - ${max:N0} {currency}";
        }

        // Handle single value
        if (valueEl.ValueKind == JsonValueKind.Number)
        {
            var val = valueEl.GetDecimal();
            var currency = salary.TryGetProperty("currency", out var currEl) ? currEl.GetString() ?? "USD" : "USD";
            return $"${val:N0} {currency}";
        }

        return string.Empty;
    }
}
