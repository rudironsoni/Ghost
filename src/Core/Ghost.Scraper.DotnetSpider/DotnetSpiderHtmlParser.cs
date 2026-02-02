using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DotnetSpider;
using DotnetSpider.DataFlow;
using DotnetSpider.DataFlow.Parser;
using DotnetSpider.DataFlow.Storage.Entity;
using DotnetSpider.Http;
using DsHttpContent = DotnetSpider.Http.ByteArrayContent;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Scraper.DotnetSpider;

/// <summary>
/// Parses HTML responses using DotnetSpider entities and converts them to JobListing objects.
/// </summary>
public sealed class DotnetSpiderHtmlParser
{
    private readonly ILogger<DotnetSpiderHtmlParser> _logger;

    public DotnetSpiderHtmlParser(ILogger<DotnetSpiderHtmlParser>? logger = null)
    {
        _logger = logger ?? NullLogger<DotnetSpiderHtmlParser>.Instance;
    }

    private static readonly Action<ILogger, string, Exception?> EmptyHtmlLogAction =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1001, "EmptyHtml"), "HTML content is empty for parsing from {Platform}");

    private static readonly Action<ILogger, string, string, Exception?> ParsingStartLogAction =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1002, "ParsingStart"), "Parsing HTML for {Platform} using DataParser<{EntityType}>");

    private static readonly Action<ILogger, string, Exception?> NoEntitiesParsedLogAction =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1003, "NoEntitiesParsed"), "No entities parsed from HTML for {Platform}");

    private static readonly Action<ILogger, int, string, Exception?> SuccessfulParseLogAction =
        LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(1004, "SuccessfulParse"), "Successfully parsed {Count} entities from {Platform}");

    private static readonly Action<ILogger, int, string, Exception?> ConvertedEntitiesLogAction =
        LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(1005, "ConvertedEntities"), "Converted {Count} entities to JobListing objects from {Platform}");

    private static readonly Action<ILogger, string, string, Exception> ParsingErrorLogAction =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1006, "ParsingError"), "Error parsing HTML for {Platform}: {ExceptionMessage}");

    private static readonly Action<ILogger, string, string, string, Exception?> IncompleteJobLogAction =
        LoggerMessage.Define<string, string, string>(LogLevel.Debug, new EventId(1007, "IncompleteJob"), "Skipping incomplete job listing from {Platform}: Title={Title}, Company={Company}");

    private static readonly Action<ILogger, string, string, Exception?> ConvertedJobLogAction =
        LoggerMessage.Define<string, string>(LogLevel.Debug, new EventId(1008, "ConvertedJob"), "Successfully converted entity to JobListing: {Title} at {Company}");

    private static readonly Action<ILogger, string, Exception> ConversionErrorLogAction =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1009, "ConversionError"), "Failed to convert entity to JobListing from {Platform}");

    private static readonly Action<ILogger, string, Exception?> UnparsedDateLogAction =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(1010, "UnparsedDate"), "Could not parse date string: {DateStr}. Using current time.");

    private static readonly Action<ILogger, string, Exception?> ParsingFailureLogAction =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(1011, "ParsingFailure"), "Parsing failed for {Platform} and no fallback parser provided");

    private static readonly Action<ILogger, string, Exception?> FallbackAttemptLogAction =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(1012, "FallbackAttempt"), "Attempting fallback parsing for {Platform}");

    private static readonly Action<ILogger, int, string, Exception?> FallbackResultsLogAction =
        LoggerMessage.Define<int, string>(LogLevel.Information, new EventId(1013, "FallbackResults"), "Fallback parser returned {Count} results for {Platform}");

    private static readonly Action<ILogger, string, string, Exception> FallbackErrorLogAction =
        LoggerMessage.Define<string, string>(LogLevel.Error, new EventId(1014, "FallbackError"), "Fallback parser also failed for {Platform}: {ExceptionMessage}");

    private static readonly Action<ILogger, string, Exception> EntityParsingErrorLogAction =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(1015, "EntityParsingError"), "Error parsing entities from HTML: {ExceptionMessage}");

    /// <summary>
    /// Parses HTML content using a DotnetSpider entity parser and converts results to JobListing objects.
    /// </summary>
    /// <typeparam name="TEntity">The DotnetSpider entity type to parse with.</typeparam>
    /// <param name="html">The HTML content to parse.</param>
    /// <param name="parser">The DataParser instance configured for the entity type.</param>
    /// <param name="sourcePlatform">The source platform name (e.g., "Indeed", "Glassdoor").</param>
    /// <param name="urlBaseFormatter">Optional function to format job URLs.</param>
    /// <param name="fallbackParser">Optional fallback parser to use if primary parsing fails.</param>
    /// <returns>A list of parsed JobListing objects.</returns>
    public async Task<List<JobListing>> ParseHtmlAsync<TEntity>(
        string html,
        DataParser<TEntity> parser,
        string sourcePlatform,
        Func<TEntity, string>? urlBaseFormatter = null,
        Func<string, Task<List<JobListing>>>? fallbackParser = null) where TEntity : EntityBase<TEntity>, new()
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            EmptyHtmlLogAction(_logger, sourcePlatform, null);
            return new List<JobListing>();
        }

        try
        {
            ParsingStartLogAction(_logger, sourcePlatform, typeof(TEntity).Name, null);

            var entities = await ParseEntitiesAsync(html, parser);

            if (entities.Count == 0)
            {
                NoEntitiesParsedLogAction(_logger, sourcePlatform, null);
                return await HandleParsingFailure(html, fallbackParser, sourcePlatform);
            }

            SuccessfulParseLogAction(_logger, entities.Count, sourcePlatform, null);

            var jobListings = ConvertEntitiesToJobListings(entities, sourcePlatform, urlBaseFormatter);

            ConvertedEntitiesLogAction(_logger, jobListings.Count, sourcePlatform, null);

            return jobListings;
        }
        catch (Exception ex)
        {
            ParsingErrorLogAction(_logger, sourcePlatform, ex.Message, ex);
            return await HandleParsingFailure(html, fallbackParser, sourcePlatform);
        }
    }

    /// <summary>
    /// Converts parsed DotnetSpider entities to JobListing objects.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to convert from.</typeparam>
    /// <param name="entities">The entities to convert.</param>
    /// <param name="sourcePlatform">The source platform name.</param>
    /// <param name="urlBaseFormatter">Optional function to format URLs.</param>
    /// <returns>A list of JobListing objects.</returns>
    public List<JobListing> ConvertEntitiesToJobListings<TEntity>(
        IEnumerable<TEntity> entities,
        string sourcePlatform,
        Func<TEntity, string>? urlBaseFormatter = null) where TEntity : EntityBase<TEntity>, new()
    {
        var jobListings = new List<JobListing>();

        foreach (var entity in entities)
        {
            try
            {
                var jobListing = ConvertEntityToJobListing(entity, sourcePlatform, urlBaseFormatter);
                
                if (string.IsNullOrWhiteSpace(jobListing.Title) || string.IsNullOrWhiteSpace(jobListing.Company))
                {
                    IncompleteJobLogAction(_logger, sourcePlatform, jobListing.Title ?? "[empty]", jobListing.Company ?? "[empty]", null);
                    continue;
                }

                jobListings.Add(jobListing);
                ConvertedJobLogAction(_logger, jobListing.Title, jobListing.Company, null);
            }
            catch (Exception ex)
            {
                ConversionErrorLogAction(_logger, sourcePlatform, ex);
                continue;
            }
        }

        return jobListings;
    }

    /// <summary>
    /// Converts a single DotnetSpider entity to a JobListing object.
    /// Handles extraction and normalization of entity properties using reflection.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to convert.</typeparam>
    /// <param name="entity">The entity to convert.</param>
    /// <param name="sourcePlatform">The source platform name.</param>
    /// <param name="urlFormatter">Optional function to format URLs.</param>
    /// <returns>A JobListing object.</returns>
    private JobListing ConvertEntityToJobListing<TEntity>(
        TEntity entity,
        string sourcePlatform,
        Func<TEntity, string>? urlFormatter = null) where TEntity : EntityBase<TEntity>, new()
    {
        var properties = entity.GetType().GetProperties();

        var id = GetPropertyValue(entity, properties, "JobKey", "JobId") ?? Guid.NewGuid().ToString();
        var title = GetPropertyValue(entity, properties, "Title") ?? string.Empty;
        var company = GetPropertyValue(entity, properties, "Company") ?? string.Empty;
        var location = GetPropertyValue(entity, properties, "Location");
        var description = GetPropertyValue(entity, properties, "Description");
        var salary = GetPropertyValue(entity, properties, "Salary");
        var jobUrl = urlFormatter?.Invoke(entity) ?? GetPropertyValue(entity, properties, "JobUrl");
        var postedAtStr = GetPropertyValue(entity, properties, "PostedAt");
        var remoteLabel = GetPropertyValue(entity, properties, "RemoteLabel");
        var jobTypeStr = GetPropertyValue(entity, properties, "JobType");

        var postedAt = ParsePostedDate(postedAtStr);
        var isRemote = IsRemotePosition(remoteLabel);
        var jobType = ParseJobType(jobTypeStr);

        return new JobListing
        {
            Id = id,
            Title = CleanText(title) ?? string.Empty,
            Company = CleanText(company) ?? string.Empty,
            Location = CleanText(location),
            Description = CleanText(description),
            Salary = CleanText(salary),
            JobType = jobType,
            ExperienceLevel = ExperienceLevel.Unknown,
            PostedAt = postedAt,
            Remote = isRemote,
            Url = CleanText(jobUrl),
            Source = sourcePlatform,
            IsEasyApply = false
        };
    }

    /// <summary>
    /// Extracts a property value from an entity using reflection, checking multiple property names.
    /// </summary>
    /// <param name="entity">The entity object to extract from.</param>
    /// <param name="properties">The cached properties array.</param>
    /// <param name="propertyNames">Property names to check in order.</param>
    /// <returns>The property value as a string, or null if not found.</returns>
    private static string? GetPropertyValue(object entity, System.Reflection.PropertyInfo[] properties, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var property = properties.FirstOrDefault(p => 
                p.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase));
            
            if (property != null)
            {
                var value = property.GetValue(entity);
                if (value != null)
                {
                    return value.ToString();
                }
            }
        }

        return null;
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
    /// Parses a job type string and returns the corresponding JobType enum value.
    /// </summary>
    private static JobType ParseJobType(string? jobTypeStr)
    {
        if (string.IsNullOrWhiteSpace(jobTypeStr))
        {
            return JobType.Unknown;
        }

        var normalized = jobTypeStr.ToLowerInvariant();

        if (normalized.Contains("full", StringComparison.OrdinalIgnoreCase) || 
            normalized.Contains("fulltime", StringComparison.OrdinalIgnoreCase))
        {
            return JobType.FullTime;
        }

        if (normalized.Contains("part", StringComparison.OrdinalIgnoreCase) || 
            normalized.Contains("parttime", StringComparison.OrdinalIgnoreCase))
        {
            return JobType.PartTime;
        }

        if (normalized.Contains("contract", StringComparison.OrdinalIgnoreCase))
        {
            return JobType.Contract;
        }

        if (normalized.Contains("internship", StringComparison.OrdinalIgnoreCase) || 
            normalized.Contains("intern", StringComparison.OrdinalIgnoreCase))
        {
            return JobType.Internship;
        }

        return JobType.Unknown;
    }

    /// <summary>
    /// Determines if a job is remote based on the remote label text.
    /// </summary>
    private static bool IsRemotePosition(string? remoteLabel)
    {
        if (string.IsNullOrWhiteSpace(remoteLabel))
        {
            return false;
        }

        var normalized = remoteLabel.ToLowerInvariant();
        return normalized.Contains("remote", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("anywhere", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("virtual", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("wfh", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("work from home", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a date string and returns a DateTimeOffset.
    /// Handles various common formats and relative dates like "3 days ago".
    /// </summary>
    private DateTimeOffset ParsePostedDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
        {
            return DateTimeOffset.UtcNow;
        }

        dateStr = dateStr.Trim().ToLowerInvariant();

        if (TryParseRelativeDate(dateStr, out var relativeDate))
        {
            return relativeDate;
        }

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
            {
                return result;
            }
        }

        UnparsedDateLogAction(_logger, dateStr, null);
        return DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Attempts to parse relative date strings like "3 days ago" or "just now".
    /// </summary>
    private static bool TryParseRelativeDate(string dateStr, out DateTimeOffset result)
    {
        result = DateTimeOffset.UtcNow;

        var match = Regex.Match(dateStr, @"(\d+)\s+(second|minute|hour|day|week|month|year)s?\s+ago", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out var count))
            {
                var unit = match.Groups[2].Value.ToLowerInvariant();
                result = unit switch
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
                return true;
            }
        }

        if (dateStr.Contains("just now") || dateStr.Contains("now"))
        {
            result = DateTimeOffset.UtcNow;
            return true;
        }

        if (dateStr.Contains("today"))
        {
            result = DateTimeOffset.UtcNow;
            return true;
        }

        if (dateStr.Contains("yesterday"))
        {
            result = DateTimeOffset.UtcNow.AddDays(-1);
            return true;
        }

        return false;
    }

     /// <summary>
     /// Parses entities from HTML content using the provided DataParser.
     /// </summary>
     /// <typeparam name="TEntity">The entity type to parse.</typeparam>
     /// <param name="html">The HTML content.</param>
     /// <param name="parser">The parser to use.</param>
     /// <returns>A list of parsed entities.</returns>
     private async Task<List<TEntity>> ParseEntitiesAsync<TEntity>(string html, DataParser<TEntity> parser)
         where TEntity : EntityBase<TEntity>, new()
     {
         try
         {
             // Create a request object with a dummy URL
             var request = new Request("https://example.com");

             // Create a response object with the HTML content
             var response = new Response
             {
                 Content = new DsHttpContent(System.Text.Encoding.UTF8.GetBytes(html)),
                 StatusCode = System.Net.HttpStatusCode.OK
             };

             // Create the DataFlowContext with null serviceProvider (acceptable for parsing-only scenarios)
             var options = new SpiderOptions();
             var context = new DataFlowContext(null, options, request, response);

             // Initialize the parser with entity configuration (schema, selectors, formatters)
             await parser.InitializeAsync();

             // Handle the context through the parser to extract entities
             await parser.HandleAsync(context, _ => Task.CompletedTask);

             // Retrieve parsed entities from context data using entity type as key
             var entityType = typeof(TEntity);
             var entities = context.GetData(entityType) as List<TEntity>;

             return entities ?? new List<TEntity>();
         }
         catch (Exception ex)
         {
             EntityParsingErrorLogAction(_logger, ex.Message, ex);
             return new List<TEntity>();
         }
     }

    /// <summary>
    /// Handles parsing failures by attempting to use a fallback parser if provided.
    /// </summary>
    /// <param name="html">The HTML content that failed to parse.</param>
    /// <param name="fallbackParser">The fallback parser to use.</param>
    /// <param name="sourcePlatform">The source platform name.</param>
    /// <returns>A list of JobListing objects from the fallback parser, or an empty list.</returns>
    private async Task<List<JobListing>> HandleParsingFailure(
        string html,
        Func<string, Task<List<JobListing>>>? fallbackParser,
        string sourcePlatform)
    {
        if (fallbackParser == null)
        {
            ParsingFailureLogAction(_logger, sourcePlatform, null);
            return new List<JobListing>();
        }

        try
        {
            FallbackAttemptLogAction(_logger, sourcePlatform, null);
            var fallbackResults = await fallbackParser(html);
            FallbackResultsLogAction(_logger, fallbackResults.Count, sourcePlatform, null);
            return fallbackResults;
        }
        catch (Exception ex)
        {
            FallbackErrorLogAction(_logger, sourcePlatform, ex.Message, ex);
            return new List<JobListing>();
        }
    }
}
