using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Ghost.Contracts.Jobs;
using Ghost.Platform.Indeed.Entities;
using Ghost.Sdk.Spider.Core.Extraction;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Platform.Indeed.Internal;

/// <summary>
/// Parser for Indeed job listings using Ghost.Sdk.Spider EntityParser.
/// This parser uses the EntityParser to extract job data from HTML content.
/// </summary>
public sealed class IndeedMultiStrategyParser
{
    private readonly ILogger<IndeedMultiStrategyParser> _logger;
    private readonly string _baseUrl;

    public IndeedMultiStrategyParser(ILogger<IndeedMultiStrategyParser>? logger = null, string baseUrl = "https://www.indeed.com")
    {
        _logger = logger ?? NullLogger<IndeedMultiStrategyParser>.Instance;
        _baseUrl = baseUrl;
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

    private static readonly Action<ILogger, Exception?> LogParseError =
        LoggerMessage.Define(LogLevel.Error, new EventId(6, nameof(LogParseError)), "Error parsing HTML");

    private static readonly Action<ILogger, Exception?> LogConversionWarning =
        LoggerMessage.Define(LogLevel.Warning, new EventId(7, nameof(LogConversionWarning)), "Failed to convert entity to JobListing");

    #endregion


    /// <summary>
    /// Main entry point for parsing HTML using Ghost.Sdk.Spider EntityParser
    /// </summary>
    public Task<List<JobListing>> ParseHtmlAsync(string html, string? baseUrl = null)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            LogEmptyHtml(_logger, null);
            return Task.FromResult(new List<JobListing>());
        }

        LogStartingParse(_logger, html.Length, null);

        try
        {
            // Create extraction context
            var context = new ExtractionContext
            {
                Content = html,
                SourceUrl = null,
                Timestamp = DateTime.UtcNow
            };

            // Parse entities using EntityParser
            var entities = EntityParser.Parse<IndeedJobEntity>(context);

            if (entities.Count == 0)
            {
                LogNoJobsExtracted(_logger, null);
                return Task.FromResult(new List<JobListing>());
            }

            // Convert entities to JobListing objects
            var jobListings = ConvertEntitiesToJobListings(entities, baseUrl ?? _baseUrl);

            LogJobsExtracted(_logger, jobListings.Count, null);

            return Task.FromResult(jobListings);
        }
        catch (Exception ex)
        {
            LogParseError(_logger, ex);
            return Task.FromResult(new List<JobListing>());
        }
    }

    /// <summary>
    /// Converts parsed entities to JobListing objects
    /// </summary>
    private List<JobListing> ConvertEntitiesToJobListings(List<IndeedJobEntity> entities, string baseUrl)
    {
        var jobListings = new List<JobListing>();

        foreach (var entity in entities)
        {
            try
            {
                // Skip incomplete jobs
                if (string.IsNullOrWhiteSpace(entity.Title) || string.IsNullOrWhiteSpace(entity.Company))
                {
                    LogIncompleteEntity(_logger, entity.Title ?? "[empty]", entity.Company ?? "[empty]", null);
                    continue;
                }

                // Construct URL from base URL and job key
                var jobUrl = string.IsNullOrWhiteSpace(entity.JobKey)
                    ? null
                    : $"{baseUrl}/viewjob?jk={entity.JobKey}";

                var jobListing = new JobListing
                {
                    Id = entity.JobKey ?? Guid.NewGuid().ToString(),
                    Title = CleanText(entity.Title) ?? string.Empty,
                    Company = CleanText(entity.Company) ?? string.Empty,
                    Location = CleanText(entity.Location),
                    Description = CleanText(entity.Description),
                    Salary = CleanText(entity.Salary),
                    JobType = ParseJobType(entity.JobType),
                    ExperienceLevel = ExperienceLevel.Unknown,
                    PostedAt = ParsePostedDate(entity.PostedAt),
                    Remote = IsRemotePosition(entity.RemoteLabel),
                    Url = jobUrl,
                    Source = "Indeed",
                    IsEasyApply = false
                };

                jobListings.Add(jobListing);
            }
            catch (Exception ex)
            {
                LogConversionWarning(_logger, ex);
                continue;
            }
        }

        return jobListings;
    }

    private static string? CleanText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        text = text.Trim();
        text = Regex.Replace(text, @"\s+", " ");
        text = text.Replace("\n", " ").Replace("\r", " ");

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
    /// Determines if a job is remote based on the remote label text
    /// </summary>
    private static bool IsRemotePosition(string? remoteLabel)
    {
        if (string.IsNullOrWhiteSpace(remoteLabel))
            return false;

        var normalized = remoteLabel.ToLowerInvariant();
        return normalized.Contains("remote", StringComparison.OrdinalIgnoreCase);
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
