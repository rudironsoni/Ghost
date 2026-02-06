using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Google.Jobs.Entities;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Platform.Google.Jobs.Internal;

/// <summary>
/// Parser for Google Jobs listings using Ghost.Sdk.Spider EntityParser.
/// This parser extracts job listings from Google Search results using attribute-based entity extraction.
/// </summary>
public sealed class GoogleJobsMultiStrategyParser
{
    private readonly EntityParser _entityParser;
    private readonly ILogger<GoogleJobsMultiStrategyParser> _logger;

    public GoogleJobsMultiStrategyParser(ILogger<GoogleJobsMultiStrategyParser>? logger = null)
    {
        _logger = logger ?? NullLogger<GoogleJobsMultiStrategyParser>.Instance;
        _entityParser = new EntityParser();
    }

    #region Logging Definitions

    private static readonly Action<ILogger, int, Exception?> LogStartingParse =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(1, nameof(LogStartingParse)), "Starting parse of {Length} bytes");

    private static readonly Action<ILogger, int, Exception?> LogJobsExtracted =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(2, nameof(LogJobsExtracted)), "Extracted {Count} jobs");

    private static readonly Action<ILogger, Exception?> LogEmptyHtml =
        LoggerMessage.Define(LogLevel.Warning, new EventId(3, nameof(LogEmptyHtml)), "ParseHtmlAsync called with empty or null HTML");

    private static readonly Action<ILogger, Exception?> LogNoJobsExtracted =
        LoggerMessage.Define(LogLevel.Warning, new EventId(4, nameof(LogNoJobsExtracted)), "No jobs extracted from HTML");

    private static readonly Action<ILogger, string, string, Exception?> LogIncompleteEntity =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(5, nameof(LogIncompleteEntity)), "Skipping incomplete job: title={Title}, company={Company}");

    private static readonly Action<ILogger, Exception?> LogConsentPageDetected =
        LoggerMessage.Define(LogLevel.Warning, new EventId(6, nameof(LogConsentPageDetected)), "Detected Google consent page - no job data available");

    #endregion

    /// <summary>
    /// Main entry point for parsing HTML using Ghost.Sdk.Spider EntityParser
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

        try
        {
            // Create extraction context
            var context = new ExtractionContext
            {
                Content = html,
                ContentType = "text/html",
                SourceUrl = "https://www.google.com/search",
                Timestamp = DateTime.UtcNow
            };

            // Parse entities using Ghost.Sdk.Spider EntityParser
            var entities = _entityParser.Parse<GoogleJobsEntity>(context);

            // Convert entities to JobListing objects
            var jobs = entities
                .Select(ConvertToJobListing)
                .Where(job => job != null && !string.IsNullOrWhiteSpace(job.Title) && !string.IsNullOrWhiteSpace(job.Company))
                .Select(job => job!).ToList();

            // Log incomplete entities
            foreach (var entity in entities)
            {
                if (string.IsNullOrWhiteSpace(entity.Title) || string.IsNullOrWhiteSpace(entity.Company))
                {
                    LogIncompleteEntity(_logger, entity.Title ?? "[empty]", entity.Company ?? "[empty]", null);
                }
            }

            if (jobs.Count > 0)
            {
                LogJobsExtracted(_logger, jobs.Count, null);
            }
            else
            {
                LogNoJobsExtracted(_logger, null);
            }

            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing HTML with EntityParser");
            return new List<JobListing>();
        }
    }

    /// <summary>
    /// Converts a GoogleJobsEntity to a JobListing
    /// </summary>
    private JobListing? ConvertToJobListing(GoogleJobsEntity entity)
    {
        if (entity == null)
            return null;

        try
        {
            var job = new JobListing
            {
                Id = entity.JobId ?? entity.Id ?? Guid.NewGuid().ToString(),
                Title = entity.Title ?? string.Empty,
                Company = entity.Company ?? string.Empty,
                Location = entity.Location,
                Description = entity.Description,
                Salary = entity.Salary,
                JobType = ParseJobType(entity.JobType),
                PostedAt = ParsePostedDate(entity.PostedAt),
                Remote = !string.IsNullOrWhiteSpace(entity.RemoteLabel),
                Url = entity.JobUrl,
                Source = "Google",
                ExperienceLevel = ExperienceLevel.Unknown,
                IsEasyApply = false
            };

            return job;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting entity to JobListing");
            return null;
        }
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
