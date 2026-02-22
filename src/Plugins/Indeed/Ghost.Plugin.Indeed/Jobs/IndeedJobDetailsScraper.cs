using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.Indeed.Jobs;

/// <summary>
/// Scraper for extracting detailed job information from Indeed job posting pages.
/// Uses browser automation to extract full details including JSON-LD structured data.
/// </summary>
public class IndeedJobDetailsScraper
{
    private readonly IBrowserSession _browserSession;
    private readonly ILogger<IndeedJobDetailsScraper> _logger;
    private readonly IJsonLdExtractor? _jsonLdExtractor;
    private readonly IndeedOptions _options;

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
        IJsonLdExtractor? jsonLdExtractor = null,
        IndeedOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(browserSession);
        ArgumentNullException.ThrowIfNull(logger);
        _browserSession = browserSession;
        _logger = logger;
        _jsonLdExtractor = jsonLdExtractor;
        _options = options ?? new IndeedOptions();
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

        IPage page = await _browserSession.NewPageAsync(null, ct).ConfigureAwait(false);
        try
        {
            string jobUrl = $"{_options.BaseUrl}/viewjob?jk={jobId}";
            await page.NavigateAsync(jobUrl, null, ct).ConfigureAwait(false);

            // Wait for the main job content to load
            await page.WaitForSelectorAsync("#jobDescriptionText, .jobsearch-JobComponent", null, ct).ConfigureAwait(false);

            // Try to extract JSON-LD structured data first (most reliable)
            JobListing? jobListing = await ExtractFromJsonLdAsync(page, jobId, ct).ConfigureAwait(false);
            if (jobListing is not null)
            {
                LogDetailsSuccess(_logger, jobId, null);
                return jobListing;
            }

            // Fallback: Extract from DOM
            jobListing = await ExtractFromDomAsync(page, jobId, jobUrl, ct).ConfigureAwait(false);
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
            await page.DisposeAsync().ConfigureAwait(false);
        }
    }

    private async Task<JobListing?> ExtractFromJsonLdAsync(
        IPage page,
        string jobId,
        CancellationToken ct)
    {
        try
        {
            if (_jsonLdExtractor is null)
            {
                return null;
            }

            string html = await page.GetContentAsync(ct).ConfigureAwait(false);
            IEnumerable<JsonElement> jsonLdElements = _jsonLdExtractor.ExtractRaw(html);

            // Find JobPosting schema
            foreach (JsonElement element in jsonLdElements)
            {
                if (element.TryGetProperty("@type", out JsonElement typeEl))
                {
                    string? typeStr = typeEl.GetString();
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

    private JobListing ParseJobPostingFromJsonLd(JsonElement jsonLd, string jobId)
    {
        return new JobListing
        {
            Id = jobId,
            Title = jsonLd.TryGetProperty("title", out JsonElement title) ? title.GetString() ?? string.Empty : string.Empty,
            Company = ExtractCompanyFromJsonLd(jsonLd),
            Location = ExtractLocationFromJsonLd(jsonLd),
            Description = jsonLd.TryGetProperty("description", out JsonElement desc) ? desc.GetString() ?? string.Empty : string.Empty,
            Salary = ExtractSalaryFromJsonLd(jsonLd),
            Url = $"{_options.BaseUrl}/viewjob?jk={jobId}",
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
        string title = await ExtractTextFromPageAsync(
            page,
            "h1.jobsearch-JobInfoHeader-title, .jobsearch-JobComponent-title, h1[class*='jobTitle']",
            ct).ConfigureAwait(false);

        // Extract company
        string company = await ExtractTextFromPageAsync(
            page,
            "[data-company-name], .jobsearch-InlineCompanyRating-companyHeader, [class*='companyName']",
            ct).ConfigureAwait(false);

        // Extract location
        string location = await ExtractTextFromPageAsync(
            page,
            "[data-testid='job-location'], .jobsearch-JobInfoHeader-subtitle-location, [class*='companyLocation']",
            ct).ConfigureAwait(false);

        // Extract description (full text)
        string description = await ExtractTextFromPageAsync(
            page,
            "#jobDescriptionText, .jobsearch-jobDescriptionText, [id*='jobDescriptionText']",
            ct).ConfigureAwait(false);

        // Extract salary
        string salary = await ExtractTextFromPageAsync(
            page,
            "#salaryInfoAndJobType, .jobsearch-JobMetadataHeader-item, [class*='salary']",
            ct).ConfigureAwait(false);

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
            IElement? element = await page.QuerySelectorAsync(selector, ct).ConfigureAwait(false);
            if (element is null)
            {
                return string.Empty;
            }

            string? text = await element.GetTextContentAsync(ct).ConfigureAwait(false);
            return text?.Trim() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ExtractCompanyFromJsonLd(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("hiringOrganization", out JsonElement org))
        {
            if (org.TryGetProperty("name", out JsonElement name))
            {
                return name.GetString() ?? string.Empty;
            }
        }

        return string.Empty;
    }

    private static string ExtractLocationFromJsonLd(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("jobLocation", out JsonElement loc))
        {
            if (loc.TryGetProperty("address", out JsonElement address))
            {
                if (address.TryGetProperty("addressLocality", out JsonElement city) &&
                    address.TryGetProperty("addressRegion", out JsonElement region))
                {
                    string cityStr = city.GetString() ?? string.Empty;
                    string regionStr = region.GetString() ?? string.Empty;
                    return $"{cityStr}, {regionStr}".Trim(',', ' ');
                }
            }
        }

        return string.Empty;
    }

    private static string ExtractSalaryFromJsonLd(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("baseSalary", out JsonElement salary))
        {
            if (salary.TryGetProperty("value", out JsonElement value))
            {
                if (value.TryGetProperty("minValue", out JsonElement min) &&
                    value.TryGetProperty("maxValue", out JsonElement max))
                {
                    string? currency = value.TryGetProperty("currency", out JsonElement curr) ? curr.GetString() : "USD";
                    return $"${min.GetDecimal()} - ${max.GetDecimal()} {currency}";
                }
                else if (value.TryGetProperty("value", out JsonElement single))
                {
                    string? currency = value.TryGetProperty("currency", out JsonElement curr) ? curr.GetString() : "USD";
                    return $"${single.GetDecimal()} {currency}";
                }
            }
        }

        return string.Empty;
    }

    private static DateTimeOffset ExtractPostedDateFromJsonLd(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("datePosted", out JsonElement datePosted))
        {
            string? dateStr = datePosted.GetString();
            if (!string.IsNullOrEmpty(dateStr) && DateTimeOffset.TryParse(dateStr, out DateTimeOffset date))
            {
                return date;
            }
        }

        return DateTimeOffset.UtcNow;
    }

    private static bool CheckIfRemote(JsonElement jsonLd)
    {
        if (jsonLd.TryGetProperty("jobLocationType", out JsonElement locType))
        {
            string? locTypeStr = locType.GetString();
            return locTypeStr?.Contains("TELECOMMUTE", StringComparison.OrdinalIgnoreCase) == true;
        }

        return false;
    }
}
