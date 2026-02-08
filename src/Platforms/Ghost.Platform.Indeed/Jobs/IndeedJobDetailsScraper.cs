using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.Indeed.Jobs;

/// <summary>
/// Scraper for extracting detailed job information from Indeed job posting pages.
/// Uses browser automation to extract full details including JSON-LD structured data.
/// </summary>
public class IndeedJobDetailsScraper
{
    private readonly IBrowserSession _browserSession;
    private readonly ILogger<IndeedJobDetailsScraper> _logger;
    private readonly IJsonLdExtractor? _jsonLdExtractor;

    private static readonly Action<ILogger, string, Exception?> LogFetchingDetails =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3101, "FetchingDetails"),
            "Fetching job details for jobId='{JobId}'");

    private static readonly Action<ILogger, string, Exception?> LogDetailsSuccess =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(3102, "DetailsSuccess"),
            "Successfully retrieved details for jobId='{JobId}'");

    private static readonly Action<ILogger, string, Exception?> LogDetailsFailed =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(3103, "DetailsFailed"),
            "Failed to retrieve details for jobId='{JobId}'");

    public IndeedJobDetailsScraper(
        IBrowserSession browserSession,
        ILogger<IndeedJobDetailsScraper> logger,
        IJsonLdExtractor? jsonLdExtractor = null)
    {
        _browserSession = browserSession ?? throw new ArgumentNullException(nameof(browserSession));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _jsonLdExtractor = jsonLdExtractor;
    }

    /// <summary>
    /// Get detailed job information by job ID.
    /// </summary>
    /// <param name="jobId">The Indeed job key (e.g., from search results)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Detailed job listing</returns>
    public async Task<JobListing> GetDetailsAsync(string jobId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jobId);

        LogFetchingDetails(_logger, jobId, null);

        var page = await _browserSession.NewPageAsync(null, ct);
        try
        {
            var jobUrl = $"https://www.indeed.com/viewjob?jk={jobId}";
            await page.NavigateAsync(jobUrl, null, ct);

            // Wait for the main job content to load
            await page.WaitForSelectorAsync("#jobDescriptionText, .jobsearch-JobComponent", null, ct);

            // Try to extract JSON-LD structured data first (most reliable)
            var jobListing = await ExtractFromJsonLdAsync(page, jobId, ct);
            if (jobListing != null)
            {
                LogDetailsSuccess(_logger, jobId, null);
                return jobListing;
            }

            // Fallback: Extract from DOM
            jobListing = await ExtractFromDomAsync(page, jobId, jobUrl, ct);
            LogDetailsSuccess(_logger, jobId, null);
            return jobListing;
        }
        catch (Exception ex)
        {
            LogDetailsFailed(_logger, jobId, ex);
            throw;
        }
        finally
        {
            await page.DisposeAsync();
        }
    }

    private async Task<JobListing?> ExtractFromJsonLdAsync(
        IPage page,
        string jobId,
        CancellationToken ct)
    {
        try
        {
            if (_jsonLdExtractor == null)
            {
                return null;
            }

            var html = await page.GetContentAsync(ct);
            var jsonLdElements = _jsonLdExtractor.ExtractRaw(html);

            // Find JobPosting schema
            foreach (var element in jsonLdElements)
            {
                if (element.TryGetProperty("@type", out var typeEl))
                {
                    var typeStr = typeEl.GetString();
                    if (typeStr == "JobPosting")
                    {
                        return ParseJobPostingFromJsonLd(element, jobId);
                    }
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static JobListing ParseJobPostingFromJsonLd(JsonElement jsonLd, string jobId)
    {
        return new JobListing
        {
            Id = jobId,
            Title = jsonLd.TryGetProperty("title", out var title) ? title.GetString() ?? string.Empty : string.Empty,
            Company = ExtractCompanyFromJsonLd(jsonLd),
            Location = ExtractLocationFromJsonLd(jsonLd),
            Description = jsonLd.TryGetProperty("description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty,
            Salary = ExtractSalaryFromJsonLd(jsonLd),
            Url = $"https://www.indeed.com/viewjob?jk={jobId}",
            Source = "Indeed",
            PostedAt = ExtractPostedDateFromJsonLd(jsonLd),
            Remote = CheckIfRemote(jsonLd)
        };
    }

    private async Task<JobListing> ExtractFromDomAsync(
        IPage page,
        string jobId,
        string jobUrl,
        CancellationToken ct)
    {
        // Extract title
        var title = await ExtractTextFromPageAsync(
            page,
            "h1.jobsearch-JobInfoHeader-title, .jobsearch-JobComponent-title, h1[class*='jobTitle']",
            ct);

        // Extract company
        var company = await ExtractTextFromPageAsync(
            page,
            "[data-company-name], .jobsearch-InlineCompanyRating-companyHeader, [class*='companyName']",
            ct);

        // Extract location
        var location = await ExtractTextFromPageAsync(
            page,
            "[data-testid='job-location'], .jobsearch-JobInfoHeader-subtitle-location, [class*='companyLocation']",
            ct);

        // Extract description (full text)
        var description = await ExtractTextFromPageAsync(
            page,
            "#jobDescriptionText, .jobsearch-jobDescriptionText, [id*='jobDescriptionText']",
            ct);

        // Extract salary
        var salary = await ExtractTextFromPageAsync(
            page,
            "#salaryInfoAndJobType, .jobsearch-JobMetadataHeader-item, [class*='salary']",
            ct);

        return new JobListing
        {
            Id = jobId,
            Title = title,
            Company = company,
            Location = location,
            Description = description,
            Salary = salary,
            Url = jobUrl,
            Source = "Indeed",
            PostedAt = DateTimeOffset.UtcNow
        };
    }

    private static async Task<string> ExtractTextFromPageAsync(
        IPage page,
        string selector,
        CancellationToken ct)
    {
        try
        {
            var element = await page.QuerySelectorAsync(selector, ct);
            if (element == null)
            {
                return string.Empty;
            }

            var text = await element.GetTextContentAsync(ct);
            return text?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractCompanyFromJsonLd(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("hiringOrganization", out var org))
        {
            if (org.TryGetProperty("name", out var name))
            {
                return name.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string ExtractLocationFromJsonLd(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("jobLocation", out var loc))
        {
            if (loc.TryGetProperty("address", out var address))
            {
                if (address.TryGetProperty("addressLocality", out var city) &&
                    address.TryGetProperty("addressRegion", out var region))
                {
                    var cityStr = city.GetString() ?? string.Empty;
                    var regionStr = region.GetString() ?? string.Empty;
                    return $"{cityStr}, {regionStr}".Trim(',', ' ');
                }
            }
        }

        return string.Empty;
    }

    private static string ExtractSalaryFromJsonLd(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("baseSalary", out var salary))
        {
            if (salary.TryGetProperty("value", out var value))
            {
                if (value.TryGetProperty("minValue", out var min) &&
                    value.TryGetProperty("maxValue", out var max))
                {
                    var currency = value.TryGetProperty("currency", out var curr) ? curr.GetString() : "USD";
                    return $"${min.GetDecimal()} - ${max.GetDecimal()} {currency}";
                }
                else if (value.TryGetProperty("value", out var single))
                {
                    var currency = value.TryGetProperty("currency", out var curr) ? curr.GetString() : "USD";
                    return $"${single.GetDecimal()} {currency}";
                }
            }
        }

        return string.Empty;
    }

    private static DateTimeOffset ExtractPostedDateFromJsonLd(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("datePosted", out var datePosted))
        {
            var dateStr = datePosted.GetString();
            if (!string.IsNullOrEmpty(dateStr) && DateTimeOffset.TryParse(dateStr, out var date))
            {
                return date;
            }
        }

        return DateTimeOffset.UtcNow;
    }

    private static bool CheckIfRemote(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("jobLocationType", out var locType))
        {
            var locTypeStr = locType.GetString();
            return locTypeStr?.Contains("TELECOMMUTE", StringComparison.OrdinalIgnoreCase) == true;
        }

        return false;
    }
}
