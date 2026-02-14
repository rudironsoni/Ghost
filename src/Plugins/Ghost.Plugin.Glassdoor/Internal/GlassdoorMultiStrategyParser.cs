using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Plugin.Glassdoor.Entities;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Plugin.Glassdoor.Internal;

/// <summary>
/// Parser for Glassdoor job listings using Ghost.Sdk.Spider framework.
/// Uses EntityParser for attribute-based extraction with fallback to JSON parser.
/// </summary>
public sealed class GlassdoorMultiStrategyParser
{
    private readonly ILogger<GlassdoorMultiStrategyParser> _logger;

    public GlassdoorMultiStrategyParser(ILogger<GlassdoorMultiStrategyParser>? logger = null)
    {
        _logger = logger ?? NullLogger<GlassdoorMultiStrategyParser>.Instance;
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

    #endregion

    /// <summary>
    /// Content classification enum for categorizing HTML content
    /// </summary>
    private enum ContentType
    {
        Unknown,
        JsonResponseFormat,
        HtmlPageFormat,
        MixedContent
    }

    /// <summary>
    /// Classifies the HTML content to determine the most appropriate parsing strategy
    /// </summary>
    private ContentType ClassifyContent(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return ContentType.Unknown;

        var trimmed = html.Trim();
        var hasJsonMarkers = trimmed.StartsWith('{') || trimmed.StartsWith('[');
        var hasHtmlMarkers = html.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
                            html.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
                            html.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                            html.Contains("<div", StringComparison.OrdinalIgnoreCase) ||
                            html.Contains("<span", StringComparison.OrdinalIgnoreCase);

        ContentType contentType = hasJsonMarkers && !hasHtmlMarkers ? ContentType.JsonResponseFormat :
                                 hasHtmlMarkers && !hasJsonMarkers ? ContentType.HtmlPageFormat :
                                 hasJsonMarkers && hasHtmlMarkers ? ContentType.MixedContent :
                                 ContentType.Unknown;

        LogContentClassification(_logger, contentType.ToString(), null);
        return contentType;
    }

    /// <summary>
    /// Primary parsing strategy using Ghost.Sdk.Spider EntityParser
    /// </summary>
    private List<JobListing>? TryStrategy1_EntityParser(string html)
    {
        LogStrategyAttempt(_logger, "EntityParser", null);

        try
        {
            var context = new ExtractionContext
            {
                Content = html,
                SourceUrl = "https://www.glassdoor.com",
                Timestamp = DateTime.UtcNow
            };

            var entities = EntityParser.Parse<GlassdoorJobEntity>(context);

            if (entities.Count > 0)
            {
                var jobs = entities
                    .Where(e => !string.IsNullOrWhiteSpace(e.Title) && !string.IsNullOrWhiteSpace(e.Company))
                    .Select(ConvertEntityToJobListing)
                    .Where(j => j != null)
                    .Cast<JobListing>()
                    .ToList();

                if (jobs.Count > 0)
                {
                    LogStrategySuccess(_logger, "EntityParser", null);
                    LogJobsExtractedFromStrategy(_logger, jobs.Count, "EntityParser", null);
                    return jobs;
                }
            }

            LogStrategyFailed(_logger, "EntityParser", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(_logger, "EntityParser", ex);
            return null;
        }
    }

    /// <summary>
    /// Converts a GlassdoorJobEntity to a JobListing
    /// </summary>
    private static JobListing? ConvertEntityToJobListing(GlassdoorJobEntity entity)
    {
        try
        {
            // Parse job type
            var jobType = ParseJobType(entity.JobType);

            // Parse posted date
            var postedAt = ParsePostedDate(entity.PostedAt);

            // Determine if remote
            var isRemote = !string.IsNullOrWhiteSpace(entity.RemoteLabel) ||
                          (entity.Location?.Contains("Remote", StringComparison.OrdinalIgnoreCase) ?? false);

            return new JobListing
            {
                Id = entity.JobId ?? Guid.NewGuid().ToString(),
                Title = entity.Title ?? string.Empty,
                Company = entity.Company ?? string.Empty,
                Location = entity.Location,
                Description = entity.Description,
                Salary = entity.Salary,
                JobType = jobType,
                PostedAt = postedAt,
                Remote = isRemote,
                Url = entity.JobUrl,
                Source = "Glassdoor",
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
    /// Secondary parsing strategy using original JSON-based parsing logic
    /// </summary>
    private List<JobListing>? TryStrategy2_OriginalJsonParser(string html)
    {
        LogStrategyAttempt(_logger, "OriginalJsonParser", null);

        try
        {
            // Check if content looks like JSON
            var trimmed = html.Trim();
            if (!trimmed.StartsWith('{') && !trimmed.StartsWith('['))
            {
                LogStrategyFailed(_logger, "OriginalJsonParser", null);
                return null;
            }

            // Try to parse as JSON and use the original parser logic
            using var doc = System.Text.Json.JsonDocument.Parse(html);
            var jobs = GlassdoorJobParser.ParseSearchResponse(html).ToList();

            if (jobs.Count > 0)
            {
                LogStrategySuccess(_logger, "OriginalJsonParser", null);
                LogJobsExtractedFromStrategy(_logger, jobs.Count, "OriginalJsonParser", null);
                return jobs;
            }

            LogStrategyFailed(_logger, "OriginalJsonParser", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(_logger, "OriginalJsonParser", ex);
            return null;
        }
    }

    /// <summary>
    /// Main entry point for parsing HTML
    /// Tries EntityParser first, then falls back to JSON parser if available
    /// </summary>
    public Task<List<JobListing>> ParseHtmlAsync(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            LogEmptyHtml(_logger, null);
            return Task.FromResult(new List<JobListing>());
        }

        LogStartingParse(_logger, html.Length, null);

        var contentType = ClassifyContent(html);

        // Strategy 1: Try Ghost.Sdk.Spider entity parser
        var jobs = TryStrategy1_EntityParser(html);
        if (jobs != null && jobs.Count > 0)
            return Task.FromResult(jobs);

        // Strategy 2: Try original JSON-based parser
        jobs = TryStrategy2_OriginalJsonParser(html);
        if (jobs != null && jobs.Count > 0)
            return Task.FromResult(jobs);

        // All strategies failed
        LogAllStrategiesFailed(_logger, 2, null);
        return Task.FromResult(new List<JobListing>());
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
}
