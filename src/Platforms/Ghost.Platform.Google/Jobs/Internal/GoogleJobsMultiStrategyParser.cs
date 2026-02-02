using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Storage.Entity;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Google.Jobs.Entities;
using Ghost.Scraper.DotnetSpider;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Platform.Google.Jobs.Internal;

/// <summary>
/// Multi-strategy parser for Google Jobs listings that attempts multiple parsing approaches:
/// 1. DotnetSpider entity parser (primary strategy)
/// 2. Original GoogleJobsParser logic (secondary strategy)
/// 3. Heuristic regex-based parsing (fallback strategy)
/// 
/// This parser provides robust parsing by gracefully falling back to alternative strategies
/// when primary approaches fail, along with structured logging and content classification.
/// </summary>
public sealed class GoogleJobsMultiStrategyParser
{
    private readonly DotnetSpiderHtmlParser _dotnetSpiderParser;
    private readonly ILogger<GoogleJobsMultiStrategyParser> _logger;

    public GoogleJobsMultiStrategyParser(ILogger<GoogleJobsMultiStrategyParser>? logger = null)
    {
        _logger = logger ?? NullLogger<GoogleJobsMultiStrategyParser>.Instance;
        _dotnetSpiderParser = new DotnetSpiderHtmlParser(logger as ILogger<DotnetSpiderHtmlParser>);
    }

    #region Logging Definitions

    private static readonly Action<ILogger, int, Exception?> LogStartingParse =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(1, nameof(LogStartingParse)), "Starting multi-strategy parse of {Length} bytes");

    private static readonly Action<ILogger, string, Exception?> LogStrategyAttempt =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(2, nameof(LogStrategyAttempt)), "Attempting parsing strategy: {Strategy}");

    private static readonly Action<ILogger, string, Exception?> LogStrategySuccess =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3, nameof(LogStrategySuccess)), "Successfully parsed jobs using strategy: {Strategy}");

    private static readonly Action<ILogger, string, Exception?> LogStrategyFailed =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(4, nameof(LogStrategyFailed)), "Strategy failed: {Strategy}");

    private static readonly Action<ILogger, string, Exception?> LogContentClassification =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(5, nameof(LogContentClassification)), "Content classified as: {ContentType}");

    private static readonly Action<ILogger, int, string, Exception?> LogJobsExtractedFromStrategy =
        LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(6, nameof(LogJobsExtractedFromStrategy)), "Extracted {Count} jobs from {Strategy} strategy");

    private static readonly Action<ILogger, Exception?> LogEmptyHtml =
        LoggerMessage.Define(LogLevel.Warning, new EventId(7, nameof(LogEmptyHtml)), "ParseHtmlAsync called with empty or null HTML");

    private static readonly Action<ILogger, int, Exception?> LogAllStrategiesFailed =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(8, nameof(LogAllStrategiesFailed)), "All {StrategyCount} strategies failed to extract jobs");

    private static readonly Action<ILogger, string, string, Exception?> LogIncompleteEntity =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(9, nameof(LogIncompleteEntity)), "Skipping incomplete job: title={Title}, company={Company}");

    private static readonly Action<ILogger, Exception?> LogConsentPageDetected =
        LoggerMessage.Define(LogLevel.Warning, new EventId(10, nameof(LogConsentPageDetected)), "Detected Google consent page - no job data available");

    private static readonly Action<ILogger, string, Exception?> LogWidgetKeyDetected =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(11, nameof(LogWidgetKeyDetected)), "Detected widget key: {WidgetKey}");

    #endregion

    /// <summary>
    /// Content classification enum for categorizing HTML content
    /// </summary>
    private enum ContentType
    {
        Unknown,
        JsonResponseFormat,
        HtmlPageFormat,
        WidgetData,
        MixedContent
    }

    /// <summary>
    /// Classifies the HTML content to determine the most appropriate parsing strategy
    /// </summary>
    private ContentType ClassifyContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return ContentType.Unknown;

        // Check for consent page
        if (html.Contains("consent.google.com", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("Before you continue to Google Search", StringComparison.OrdinalIgnoreCase))
            return ContentType.Unknown;

        var trimmed = html.Trim();
        var hasJsonMarkers = trimmed.StartsWith('{') || trimmed.StartsWith('[');
        var hasHtmlMarkers = html.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                            html.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
                            html.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                            html.Contains("<div", StringComparison.OrdinalIgnoreCase) ||
                            html.Contains("<span", StringComparison.OrdinalIgnoreCase);
        var hasWidgetData = html.Contains("data-ved", StringComparison.OrdinalIgnoreCase) ||
                           html.Contains("AF_initDataCallback", StringComparison.OrdinalIgnoreCase) ||
                           html.Contains("gws-plugins-horizon-jobs", StringComparison.OrdinalIgnoreCase);

        ContentType contentType = hasJsonMarkers && !hasHtmlMarkers ? ContentType.JsonResponseFormat :
                                 hasHtmlMarkers && !hasJsonMarkers && hasWidgetData ? ContentType.WidgetData :
                                 hasHtmlMarkers && !hasJsonMarkers ? ContentType.HtmlPageFormat :
                                 hasJsonMarkers && hasHtmlMarkers ? ContentType.MixedContent :
                                 ContentType.Unknown;

        LogContentClassification(_logger, contentType.ToString(), null);
        return contentType;
    }

    /// <summary>
    /// Primary parsing strategy using DotnetSpider entity parser (GoogleJobsEntity)
    /// </summary>
    private async Task<List<JobListing>?> TryStrategy1_DotnetSpiderEntityParser(string html)
    {
        LogStrategyAttempt(_logger, "DotnetSpiderEntityParser", null);

        try
        {
            var parser = new DataParser<GoogleJobsEntity>();
            var jobs = await _dotnetSpiderParser.ParseHtmlAsync(
                html,
                parser,
                "Google",
                null,
                null);

            if (jobs.Count > 0)
            {
                LogStrategySuccess(_logger, "DotnetSpiderEntityParser", null);
                LogJobsExtractedFromStrategy(_logger, jobs.Count, "DotnetSpiderEntityParser", null);
                return jobs;
            }

            LogStrategyFailed(_logger, "DotnetSpiderEntityParser", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(_logger, "DotnetSpiderEntityParser", ex);
            return null;
        }
    }

    /// <summary>
    /// Secondary parsing strategy using original GoogleJobsParser logic
    /// </summary>
    private List<JobListing>? TryStrategy2_OriginalGoogleJobsParser(string html)
    {
        LogStrategyAttempt(_logger, "OriginalGoogleJobsParser", null);

        try
        {
            var jobs = GoogleJobsParser.ParseFromHtml(html, _logger).ToList();

            if (jobs.Count > 0)
            {
                LogStrategySuccess(_logger, "OriginalGoogleJobsParser", null);
                LogJobsExtractedFromStrategy(_logger, jobs.Count, "OriginalGoogleJobsParser", null);
                return jobs;
            }

            LogStrategyFailed(_logger, "OriginalGoogleJobsParser", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(_logger, "OriginalGoogleJobsParser", ex);
            return null;
        }
    }

    /// <summary>
    /// Tertiary fallback strategy using heuristic regex-based parsing for HTML widget patterns
    /// </summary>
    private List<JobListing> TryStrategy3_HeuristicRegexParser(string html)
    {
        LogStrategyAttempt(_logger, "HeuristicRegexParser", null);

        var jobs = new List<JobListing>();

        try
        {
            // Pattern to match Google Jobs widget listing containers
            var jobPattern = @"<div[^>]*(?:role\s*=\s*['""]listitem['""]|class\s*=\s*['""][^'""]*gws-plugins-horizon-jobs__li[^'""]*['""])[^>]*>.*?(?=<div[^>]*(?:role\s*=\s*['""]listitem['""]|class\s*=\s*['""][^'""]*gws-plugins-horizon-jobs__li[^'""]*['""])|</div>)";
            var matches = Regex.Matches(html, jobPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (matches.Count == 0)
            {
                LogStrategyFailed(_logger, "HeuristicRegexParser", null);
                return jobs;
            }

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var jobHtml = match.Value;
                var job = ExtractJobFromHtml(jobHtml);

                if (job != null && !string.IsNullOrWhiteSpace(job.Title) && !string.IsNullOrWhiteSpace(job.Company))
                {
                    jobs.Add(job);
                }
                else if (job != null)
                {
                    LogIncompleteEntity(_logger, job.Title ?? "[empty]", job.Company ?? "[empty]", null);
                }
            }

            if (jobs.Count > 0)
            {
                LogStrategySuccess(_logger, "HeuristicRegexParser", null);
                LogJobsExtractedFromStrategy(_logger, jobs.Count, "HeuristicRegexParser", null);
                return jobs;
            }

            LogStrategyFailed(_logger, "HeuristicRegexParser", null);
            return jobs;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(_logger, "HeuristicRegexParser", ex);
            return jobs;
        }
    }

    /// <summary>
    /// Extracts a single job from HTML widget using heuristic regex patterns
    /// </summary>
    private static JobListing? ExtractJobFromHtml(string jobHtml)
    {
        try
        {
            // Extract job ID from data-ved or data-id attributes
            var jobIdMatch = Regex.Match(jobHtml, @"data-ved\s*=\s*['""]([^'""]+)['""]|data-id\s*=\s*['""]([^'""]+)['""]|data-job-id\s*=\s*['""]([^'""]+)['""]", RegexOptions.IgnoreCase);
            var jobId = jobIdMatch.Success ? 
                jobIdMatch.Groups[1].Value ?? jobIdMatch.Groups[2].Value ?? jobIdMatch.Groups[3].Value ?? Guid.NewGuid().ToString() :
                Guid.NewGuid().ToString();

            // Extract title - look for h3 or elements with role='heading'
            var titleMatch = Regex.Match(jobHtml, @"<h3[^>]*>.*?<.*?>([^<]+)</.*?></h3>|<[^>]*role\s*=\s*['""]heading['""][^>]*>([^<]+)<", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            var title = titleMatch.Success ? CleanHtmlText(titleMatch.Groups[1].Value ?? titleMatch.Groups[2].Value) : null;

            if (string.IsNullOrWhiteSpace(title))
                return null;

            // Extract company
            var companyMatch = Regex.Match(jobHtml, @"<.*?class\s*=\s*['""][^'""]*vNEEBe[^'""]*['""][^>]*>([^<]+)<|<.*?class\s*=\s*['""][^'""]*Employer[^'""]*['""][^>]*>([^<]+)<", RegexOptions.IgnoreCase);
            var company = companyMatch.Success ? CleanHtmlText(companyMatch.Groups[1].Value ?? companyMatch.Groups[2].Value) : null;

            // Extract location
            var locationMatch = Regex.Match(jobHtml, @"<.*?class\s*=\s*['""][^'""]*Qk3sIe[^'""]*['""][^>]*>([^<]+)<|<.*?class\s*=\s*['""][^'""]*location[^'""]*['""][^>]*>([^<]+)<", RegexOptions.IgnoreCase);
            var location = locationMatch.Success ? CleanHtmlText(locationMatch.Groups[1].Value ?? locationMatch.Groups[2].Value) : null;

            // Extract salary
            var salaryMatch = Regex.Match(jobHtml, @"<.*?class\s*=\s*['""][^'""]*salary[^'""]*['""][^>]*>([^<]+)<", RegexOptions.IgnoreCase);
            var salary = salaryMatch.Success ? CleanHtmlText(salaryMatch.Groups[1].Value) : null;

            // Extract description - look for snippet or job description patterns
            var descriptionMatch = Regex.Match(jobHtml, @"<.*?class\s*=\s*['""][^'""]*HBvzbc[^'""]*['""][^>]*>([^<]+)<|<.*?class\s*=\s*['""][^'""]*snippet[^'""]*['""][^>]*>([^<]+)<", RegexOptions.IgnoreCase);
            var description = descriptionMatch.Success ? CleanHtmlText(descriptionMatch.Groups[1].Value ?? descriptionMatch.Groups[2].Value) : null;

            // Extract job URL
            var urlMatch = Regex.Match(jobHtml, @"<a[^>]*href\s*=\s*['""]([^'""]*jobs[^'""]*)['""]", RegexOptions.IgnoreCase);
            var jobUrl = urlMatch.Success ? 
                (urlMatch.Groups[1].Value.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? urlMatch.Groups[1].Value : "https://www.google.com" + urlMatch.Groups[1].Value) :
                null;

            // Extract posted date
            var postedAtMatch = Regex.Match(jobHtml, @"<.*?class\s*=\s*['""][^'""]*date[^'""]*['""][^>]*>([^<]+)<", RegexOptions.IgnoreCase);
            var postedAtStr = postedAtMatch.Success ? CleanHtmlText(postedAtMatch.Groups[1].Value) : null;
            var postedAt = ParsePostedDate(postedAtStr);

            // Extract job type
            var jobTypeMatch = Regex.Match(jobHtml, @"<.*?class\s*=\s*['""][^'""]*job-type[^'""]*['""][^>]*>([^<]+)<|<.*?class\s*=\s*['""][^'""]*employment[^'""]*['""][^>]*>([^<]+)<", RegexOptions.IgnoreCase);
            var jobTypeStr = jobTypeMatch.Success ? CleanHtmlText(jobTypeMatch.Groups[1].Value ?? jobTypeMatch.Groups[2].Value) : null;
            var jobType = ParseJobType(jobTypeStr);

            // Check for remote
            var isRemote = jobHtml.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                          jobHtml.Contains("Work from home", StringComparison.OrdinalIgnoreCase);

            return new JobListing
            {
                Id = jobId,
                Title = title ?? string.Empty,
                Company = company ?? string.Empty,
                Location = location,
                Description = description,
                Salary = salary,
                JobType = jobType,
                PostedAt = postedAt,
                Remote = isRemote,
                Url = jobUrl,
                Source = "Google",
                ExperienceLevel = ExperienceLevel.Unknown,
                IsEasyApply = false
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Cleans HTML text by removing tags and decoding entities
    /// </summary>
    private static string? CleanHtmlText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        // Remove HTML tags
        text = Regex.Replace(text, "<[^>]+>", string.Empty);

        // Decode HTML entities
        text = System.Net.WebUtility.HtmlDecode(text);

        // Normalize whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// Parses a job type string and returns the corresponding JobType enum value
    /// </summary>
    private static JobType ParseJobType(string? jobTypeStr)
    {
        if (string.IsNullOrWhiteSpace(jobTypeStr))
            return JobType.Unknown;

        var normalized = jobTypeStr.ToLowerInvariant();

        if (normalized.Contains("full", StringComparison.OrdinalIgnoreCase))
            return JobType.FullTime;

        if (normalized.Contains("part", StringComparison.OrdinalIgnoreCase))
            return JobType.PartTime;

        if (normalized.Contains("contract", StringComparison.OrdinalIgnoreCase))
            return JobType.Contract;

        if (normalized.Contains("intern", StringComparison.OrdinalIgnoreCase))
            return JobType.Internship;

        return JobType.Unknown;
    }

    /// <summary>
    /// Parses a date string and returns a DateTimeOffset
    /// Handles relative dates like "3 days ago" and absolute timestamps
    /// </summary>
    private static DateTimeOffset ParsePostedDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return DateTimeOffset.UtcNow;

        dateStr = dateStr.Trim().ToLowerInvariant();

        // Try to parse relative dates
        var relativeMatch = Regex.Match(dateStr, @"(\d+)\s+(second|minute|hour|day|week|month|year)s?\s+ago", RegexOptions.IgnoreCase);
        if (relativeMatch.Success && int.TryParse(relativeMatch.Groups[1].Value, out var count))
        {
            var unit = relativeMatch.Groups[2].Value.ToLowerInvariant();
            return unit switch
            {
                "second" => DateTimeOffset.UtcNow.AddSeconds(-count),
                "minute" => DateTimeOffset.UtcNow.AddMinutes(-count),
                "hour" => DateTimeOffset.UtcNow.AddHours(-count),
                "day" => DateTimeOffset.UtcNow.AddDays(-count),
                "week" => DateTimeOffset.UtcNow.AddDays(-count * 7),
                "month" => DateTimeOffset.UtcNow.AddMonths(-count),
                "year" => DateTimeOffset.UtcNow.AddYears(-count),
                _ => DateTimeOffset.UtcNow
            };
        }

        // Handle special cases
        if (dateStr.Contains("just now") || dateStr.Contains("today"))
            return DateTimeOffset.UtcNow;

        if (dateStr.Contains("yesterday"))
            return DateTimeOffset.UtcNow.AddDays(-1);

        // Try to parse as absolute date
        var formats = new[]
        {
            "yyyy-MM-dd",
            "yyyy-MM-dd HH:mm:ss",
            "yyyy-MM-ddTHH:mm:ss",
            "yyyy-MM-ddTHH:mm:ssZ",
            "yyyy-MM-ddTHH:mm:ss.fffZ",
            "MM/dd/yyyy",
            "dd/MM/yyyy",
            "MMM dd, yyyy",
            "MMMM dd, yyyy"
        };

        foreach (var format in formats)
        {
            if (DateTimeOffset.TryParseExact(dateStr, format, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var result))
                return result;
        }

        return DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Main entry point for parsing HTML using multi-strategy approach
    /// Tries strategies in order: DotnetSpider → OriginalGoogleJobsParser → HeuristicRegex
    /// </summary>
    public async Task<List<JobListing>> ParseHtmlAsync(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            LogEmptyHtml(_logger, null);
            return new List<JobListing>();
        }

        LogStartingParse(_logger, html.Length, null);

        // Check for consent page early
        if (html.Contains("consent.google.com", StringComparison.OrdinalIgnoreCase) ||
            html.Contains("Before you continue to Google Search", StringComparison.OrdinalIgnoreCase))
        {
            LogConsentPageDetected(_logger, null);
            return new List<JobListing>();
        }

        var contentType = ClassifyContent(html);

        // Strategy 1: Try DotnetSpider entity parser
        var jobs = await TryStrategy1_DotnetSpiderEntityParser(html);
        if (jobs != null && jobs.Count > 0)
            return jobs;

        // Strategy 2: Try original GoogleJobsParser logic
        jobs = TryStrategy2_OriginalGoogleJobsParser(html);
        if (jobs != null && jobs.Count > 0)
            return jobs;

        // Strategy 3: Fall back to heuristic regex parsing
        jobs = TryStrategy3_HeuristicRegexParser(html);
        if (jobs.Count > 0)
            return jobs;

        // All strategies failed
        LogAllStrategiesFailed(_logger, 3, null);
        return new List<JobListing>();
    }
}
