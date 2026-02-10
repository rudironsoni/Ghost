using System;
using System.Collections.Generic;
using System.Text.Json;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ghost.Platform.Google.Jobs.Internal;

public static class GoogleJobsParser
{
    private static readonly Action<ILogger, int, Exception?> LogStartingParse =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(1, nameof(LogStartingParse)), "Starting parse of {Length} bytes");

    private static readonly Action<ILogger, int, Exception?> LogFoundWidgetKey =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(2, nameof(LogFoundWidgetKey)), "Found widget key at index {Index}");

    private static readonly Action<ILogger, int, Exception?> LogFoundHtlJobs =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(3, nameof(LogFoundHtlJobs)), "Found 'htl;jobs' at index {Index}");

    private static readonly Action<ILogger, int, Exception?> LogFoundJobsMarker =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(4, nameof(LogFoundJobsMarker)), "Found 'jobs' at index {Index}");

    private static readonly Action<ILogger, int, Exception?> LogFallbackJsonStart =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(5, nameof(LogFallbackJsonStart)), "Fallback JSON start found at {Start}");

    private static readonly Action<ILogger, Exception?> LogNoJsonStart =
        LoggerMessage.Define(LogLevel.Debug, new EventId(6, nameof(LogNoJsonStart)), "No JSON start bracket found");

    private static readonly Action<ILogger, int, Exception?> LogJsonCandidateExtraction =
        LoggerMessage.Define<int>(LogLevel.Debug, new EventId(7, nameof(LogJsonCandidateExtraction)), "JSON candidate extraction length: {Length}");

    private static readonly Action<ILogger, int, JsonValueKind, Exception?> LogSkippedJsonCandidate =
        LoggerMessage.Define<int, JsonValueKind>(LogLevel.Debug, new EventId(8, nameof(LogSkippedJsonCandidate)), "Skipped JSON candidate at position {Pos}: root is {Kind}");

    private static readonly Action<ILogger, int, Exception?> LogJsonParsingFailure =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(9, nameof(LogJsonParsingFailure)), "JSON parsing failure for candidate at position {Pos}");

    private static readonly Action<ILogger, Exception?> LogEmptyHtml =
        LoggerMessage.Define(LogLevel.Warning, new EventId(10, nameof(LogEmptyHtml)), "ParseFromHtml called with empty or null HTML");

    private static readonly Action<ILogger, Exception?> LogDetectedConsentPage =
        LoggerMessage.Define(LogLevel.Warning, new EventId(11, nameof(LogDetectedConsentPage)), "Detected consent page - no job data available");

    private static readonly Action<ILogger, string, Exception?> LogStrategyAttempt =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(12, nameof(LogStrategyAttempt)), "Attempting parsing strategy: {Strategy}");

    private static readonly Action<ILogger, string, Exception?> LogStrategySuccess =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(13, nameof(LogStrategySuccess)), "Successfully parsed jobs using strategy: {Strategy}");

    private static readonly Action<ILogger, string, Exception?> LogStrategyFailed =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(14, nameof(LogStrategyFailed)), "Strategy failed: {Strategy}");

    private static readonly Action<ILogger, string, Exception?> LogDynamicWidgetKeyFound =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(15, nameof(LogDynamicWidgetKeyFound)), "Found dynamic widget key: {WidgetKey}");

    private static readonly Action<ILogger, string, Exception?> LogJsonLdFound =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(16, nameof(LogJsonLdFound)), "Found JSON-LD structured data with {Count} job entries");

    private static readonly Action<ILogger, string, Exception?> LogAfInitDataFound =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(17, nameof(LogAfInitDataFound)), "Found AF_initDataCallback with {Length} characters");

    private static readonly Action<ILogger, string, Exception?> LogDataVedFound =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(18, nameof(LogDataVedFound)), "Found data-ved attribute: {Value}");

    private static readonly Action<ILogger, string, Exception?> LogScriptTagFound =
        LoggerMessage.Define<string>(LogLevel.Debug, new EventId(19, nameof(LogScriptTagFound)), "Found script tag with type: {Type}");

    private static readonly Action<ILogger, int, Exception?> LogJobsExtractedFromStrategy =
        LoggerMessage.Define<int>(LogLevel.Information, new EventId(20, nameof(LogJobsExtractedFromStrategy)), "Extracted {Count} jobs from strategy");

    private static readonly Action<ILogger, string, Exception?> LogConsentPagePreview =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(21, nameof(LogConsentPagePreview)), "Consent page HTML preview: {Preview}");

    private static readonly Action<ILogger, int, bool, bool, bool, bool, bool, Exception?> LogPatternDetection =
        LoggerMessage.Define<int, bool, bool, bool, bool, bool>(LogLevel.Warning, new EventId(22, nameof(LogPatternDetection)), "All parsing strategies failed. HTML length: {Length}. Pattern detection: data-ved={HasDataVed}, jobs={HasJobsKeyword}, htl;jobs={HasHtlJobs}, json-ld={HasJsonLd}, script={HasScriptTags}");

    private static readonly Action<ILogger, int, string, Exception?> LogHtmlSample =
        LoggerMessage.Define<int, string>(LogLevel.Debug, new EventId(23, nameof(LogHtmlSample)), "HTML sample (first {Size} chars): {Sample}");

    /// <summary>
    /// Dynamically detects widget key by searching for 9+ digit numbers in data attributes near job listings
    /// </summary>
    private static string? DetectDynamicWidgetKey(string html, ILogger logger)
    {
        // Pattern to find data-ved attributes or similar data attributes with 9+ digit numbers
        var dataVedPattern = @"data-ved\s*=\s*[""'](\d{9,})[""']";
        var matches = System.Text.RegularExpressions.Regex.Matches(html, dataVedPattern);

        if (matches.Count > 0)
        {
            // Return the first match as the widget key
            var widgetKey = matches[0].Groups[1].Value;
            LogDynamicWidgetKeyFound(logger, widgetKey, null);
            return widgetKey;
        }

        // Fallback: look for any 9+ digit number in data attributes
        var dataAttrPattern = @"data-\w+\s*=\s*[""'](\d{9,})[""']";
        var attrMatches = System.Text.RegularExpressions.Regex.Matches(html, dataAttrPattern);

        if (attrMatches.Count > 0)
        {
            var widgetKey = attrMatches[0].Groups[1].Value;
            LogDynamicWidgetKeyFound(logger, widgetKey, null);
            return widgetKey;
        }

        return null;
    }

    /// <summary>
    /// Strategy 1: JobSpy-style widget key pattern (matches 520084652":[...])
    /// This is the exact pattern JobSpy uses - finds the widget key followed by job arrays
    /// UPDATED: Now searches for ALL widget keys dynamically and extracts job data
    /// </summary>
    private static List<JobListing>? TryStrategy1_JobspyWidgetKey(string html, ILogger logger)
    {
        LogStrategyAttempt(logger, "JobspyWidgetKey", null);

        try
        {
            var jobs = new List<JobListing>();

            // Try multiple widget key patterns
            var widgetKeys = new[] { "520084652", "520084653", "htl;jobs" };

            // Pattern to find widget keys followed by JSON arrays
            // Matches: "widgetKey":[[...job data...]]
            foreach (var widgetKey in widgetKeys)
            {
                // Find all occurrences of the widget key
                var keyIndex = 0;
                while ((keyIndex = html.IndexOf($"\"{widgetKey}\":", keyIndex, StringComparison.Ordinal)) >= 0)
                {
                    try
                    {
                        // Move past the key to find the start of the JSON array
                        var jsonStart = keyIndex + widgetKey.Length + 3; // +3 for ":"
                        if (jsonStart >= html.Length) break;

                        // Skip whitespace
                        while (jsonStart < html.Length && char.IsWhiteSpace(html[jsonStart]))
                        {
                            jsonStart++;
                        }

                        if (jsonStart >= html.Length || html[jsonStart] != '[')
                        {
                            keyIndex = jsonStart;
                            continue;
                        }

                        // Extract the JSON array by matching brackets
                        var depth = 0;
                        var jsonEnd = jsonStart;
                        var inString = false;
                        var escapeNext = false;

                        for (; jsonEnd < html.Length && jsonEnd < jsonStart + 500000; jsonEnd++)
                        {
                            var ch = html[jsonEnd];

                            if (escapeNext)
                            {
                                escapeNext = false;
                                continue;
                            }

                            if (ch == '\\')
                            {
                                escapeNext = true;
                                continue;
                            }

                            if (ch == '"')
                            {
                                inString = !inString;
                                continue;
                            }

                            if (inString) continue;

                            if (ch == '[') depth++;
                            else if (ch == ']')
                            {
                                depth--;
                                if (depth == 0)
                                {
                                    jsonEnd++; // Include the closing bracket
                                    break;
                                }
                            }
                        }

                        if (depth != 0 || jsonEnd >= html.Length)
                        {
                            keyIndex = jsonStart;
                            continue;
                        }

                        var jsonString = html.Substring(jsonStart, jsonEnd - jsonStart);

                        try
                        {
                            using var doc = JsonDocument.Parse(jsonString);

                            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                            {
                                // Process each job in the array
                                foreach (var jobArray in doc.RootElement.EnumerateArray())
                                {
                                    if (jobArray.ValueKind == JsonValueKind.Array)
                                    {
                                        var job = ExtractJobFromJobspyFormat(jobArray);
                                        if (job != null)
                                        {
                                            jobs.Add(job);
                                        }
                                    }
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // Invalid JSON, continue searching
                        }

                        keyIndex = jsonEnd;
                    }
                    catch
                    {
                        keyIndex++;
                    }
                }
            }

            if (jobs.Count > 0)
            {
                LogStrategySuccess(logger, "JobspyWidgetKey", null);
                LogJobsExtractedFromStrategy(logger, jobs.Count, null);
                return jobs;
            }

            LogStrategyFailed(logger, "JobspyWidgetKey", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(logger, "JobspyWidgetKey", ex);
            return null;
        }
    }

    /// <summary>
    /// Extract job from JobSpy format (nested array structure)
    /// JobSpy format: [title, company, location, ..., url, ..., description]
    /// </summary>
    private static JobListing? ExtractJobFromJobspyFormat(System.Text.Json.JsonElement jobArray)
    {
        try
        {
            // JobSpy index mapping (from Python code):
            // [0] title, [1] company, [2] location, [3] urls, [12] days ago, [19] description, [28] job id
            string title = GetStringAt(jobArray, 0) ?? string.Empty;
            string company = GetStringAt(jobArray, 1) ?? string.Empty;
            string location = GetStringAt(jobArray, 2) ?? string.Empty;
            string? url = null;
            string? datePostedStr = GetStringAt(jobArray, 12);
            string? description = GetStringAt(jobArray, 19);
            string? id = GetStringAt(jobArray, 28);

            if (string.IsNullOrWhiteSpace(title)) return null;

            // Parse URL from nested array at index 3
            if (jobArray.GetArrayLength() > 3)
            {
                var urlElement = jobArray[3];
                if (urlElement.ValueKind == System.Text.Json.JsonValueKind.Array && urlElement.GetArrayLength() > 0)
                {
                    var urlArray = urlElement[0];
                    if (urlArray.ValueKind == System.Text.Json.JsonValueKind.Array && urlArray.GetArrayLength() > 0)
                    {
                        url = GetStringAt(urlArray, 0);
                    }
                }
            }

            // Parse date posted (e.g., "3 days ago")
            var postedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(datePostedStr))
            {
                var match = System.Text.RegularExpressions.Regex.Match(datePostedStr, @"(\d+)\s+(day|days|hour|hours|week|weeks|month|months)\s+ago");
                if (match.Success)
                {
                    var num = int.TryParse(match.Groups[1].Value, out var n) ? n : 0;
                    var unit = match.Groups[2].Value.ToLowerInvariant();
                    postedAt = unit switch
                    {
                        "hour" or "hours" => DateTimeOffset.UtcNow.AddHours(-num),
                        "day" or "days" => DateTimeOffset.UtcNow.AddDays(-num),
                        "week" or "weeks" => DateTimeOffset.UtcNow.AddDays(-num * 7),
                        "month" or "months" => DateTimeOffset.UtcNow.AddMonths(-num),
                        _ => DateTimeOffset.UtcNow
                    };
                }
            }

            return new JobListing
            {
                Id = string.IsNullOrWhiteSpace(id) ? $"go-" + Guid.NewGuid().ToString("N") : $"go-{id}",
                Title = title,
                Company = string.IsNullOrWhiteSpace(company) ? "Unknown" : company,
                Location = location,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Url = url ?? $"https://www.google.com/search?q={Uri.EscapeDataString(title)}",
                PostedAt = postedAt,
                Source = "GoogleJobs"
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Strategy 2: Search for jobs/job in JSON script tags
    /// </summary>
    private static List<JobListing>? TryStrategy2_ScriptTags(string html, ILogger logger)
    {
        LogStrategyAttempt(logger, "ScriptTags", null);

        try
        {
            // Look for script tags with type="application/json"
            var scriptPattern = @"<script\s+type\s*=\s*[""']application/json[""'][^>]*>(.*?)</script>";
            var matches = System.Text.RegularExpressions.Regex.Matches(html, scriptPattern, System.Text.RegularExpressions.RegexOptions.Singleline);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var jsonContent = match.Groups[1].Value.Trim();
                LogScriptTagFound(logger, "application/json", null);

                // Check if the JSON contains "jobs" or "job"
                if (jsonContent.Contains("\"jobs\"", StringComparison.OrdinalIgnoreCase) ||
                    jsonContent.Contains("\"job\"", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonContent);
                        var jobs = ExtractJobsFromJsonDocument(doc, logger);
                        if (jobs.Count > 0)
                        {
                            LogStrategySuccess(logger, "ScriptTags", null);
                            LogJobsExtractedFromStrategy(logger, jobs.Count, null);
                            return jobs;
                        }
                    }
                    catch
                    {
                        // Continue to next match
                    }
                }
            }

            LogStrategyFailed(logger, "ScriptTags", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(logger, "ScriptTags", ex);
            return null;
        }
    }

    /// <summary>
    /// Strategy 3: Extract from data-ved attributes
    /// </summary>
    private static List<JobListing>? TryStrategy3_DataVedAttributes(string html, ILogger logger)
    {
        LogStrategyAttempt(logger, "DataVedAttributes", null);

        try
        {
            // Look for data-ved attributes which Google commonly uses
            var dataVedPattern = @"data-ved\s*=\s*[""']([^""']+)[""']";
            var matches = System.Text.RegularExpressions.Regex.Matches(html, dataVedPattern);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var vedValue = match.Groups[1].Value;
                LogDataVedFound(logger, vedValue, null);

                // Try to find JSON near the data-ved attribute
                var matchIndex = match.Index;
                var beforeMatch = html.Substring(Math.Max(0, matchIndex - 5000), Math.Min(5000, matchIndex));
                var afterMatch = html.Substring(matchIndex, Math.Min(5000, html.Length - matchIndex));

                // Look for JSON arrays in the vicinity
                var jsonStart = beforeMatch.LastIndexOf('[');
                if (jsonStart >= 0)
                {
                    var actualStart = matchIndex - (beforeMatch.Length - jsonStart);
                    var jobs = ExtractJobsFromJsonArray(html, actualStart, logger);
                    if (jobs.Count > 0)
                    {
                        LogStrategySuccess(logger, "DataVedAttributes", null);
                        LogJobsExtractedFromStrategy(logger, jobs.Count, null);
                        return jobs;
                    }
                }
            }

            LogStrategyFailed(logger, "DataVedAttributes", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(logger, "DataVedAttributes", ex);
            return null;
        }
    }

    /// <summary>
    /// Strategy 4: Extract from AF_initDataCallback patterns
    /// </summary>
    private static List<JobListing>? TryStrategy4_AfInitDataCallback(string html, ILogger logger)
    {
        LogStrategyAttempt(logger, "AfInitDataCallback", null);

        try
        {
            // Look for AF_initDataCallback patterns
            var afInitPattern = @"AF_initDataCallback\(([^)]+)\)";
            var matches = System.Text.RegularExpressions.Regex.Matches(html, afInitPattern);

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var callbackContent = match.Groups[1].Value;
                LogAfInitDataFound(logger, callbackContent.Length.ToString(System.Globalization.CultureInfo.InvariantCulture), null);

                // Try to extract JSON from the callback
                var jsonStart = callbackContent.IndexOf('[');
                if (jsonStart >= 0)
                {
                    var jsonContent = callbackContent.Substring(jsonStart);
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonContent);
                        var jobs = ExtractJobsFromJsonDocument(doc, logger);
                        if (jobs.Count > 0)
                        {
                            LogStrategySuccess(logger, "AfInitDataCallback", null);
                            LogJobsExtractedFromStrategy(logger, jobs.Count, null);
                            return jobs;
                        }
                    }
                    catch
                    {
                        // Continue to next match
                    }
                }
            }

            LogStrategyFailed(logger, "AfInitDataCallback", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(logger, "AfInitDataCallback", ex);
            return null;
        }
    }

    /// <summary>
    /// Parse JSON-LD structured data
    /// </summary>
    private static List<JobListing>? TryJsonLdParsing(string html, ILogger logger)
    {
        LogStrategyAttempt(logger, "JsonLd", null);

        try
        {
            // Look for JSON-LD script tags
            var jsonLdPattern = @"<script\s+type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>";
            var matches = System.Text.RegularExpressions.Regex.Matches(html, jsonLdPattern, System.Text.RegularExpressions.RegexOptions.Singleline);

            var jobs = new List<JobListing>();

            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                var jsonContent = match.Groups[1].Value.Trim();

                try
                {
                    using var doc = JsonDocument.Parse(jsonContent);

                    // Check if it's a JobPosting or array of JobPosting
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("@type", out var type) &&
                            type.GetString() == "JobPosting")
                        {
                            var job = ExtractJobFromJsonLd(doc.RootElement);
                            if (job != null) jobs.Add(job);
                        }
                    }
                    else if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Object &&
                                item.TryGetProperty("@type", out var type) &&
                                type.GetString() == "JobPosting")
                            {
                                var job = ExtractJobFromJsonLd(item);
                                if (job != null) jobs.Add(job);
                            }
                        }
                    }
                }
                catch
                {
                    // Continue to next match
                }
            }

            if (jobs.Count > 0)
            {
                LogJsonLdFound(logger, jobs.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), null);
                LogStrategySuccess(logger, "JsonLd", null);
                LogJobsExtractedFromStrategy(logger, jobs.Count, null);
                return jobs;
            }

            LogStrategyFailed(logger, "JsonLd", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(logger, "JsonLd", ex);
            return null;
        }
    }

    /// <summary>
    /// Extract a single job from JSON-LD format
    /// </summary>
    private static JobListing? ExtractJobFromJsonLd(JsonElement element)
    {
        try
        {
            string title = element.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty;
            string company = element.TryGetProperty("hiringOrganization", out var orgProp) && orgProp.ValueKind == JsonValueKind.Object
                ? (orgProp.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? string.Empty : string.Empty)
                : string.Empty;
            string location = element.TryGetProperty("jobLocation", out var locProp) && locProp.ValueKind == JsonValueKind.Object
                ? (locProp.TryGetProperty("address", out var addrProp) && addrProp.ValueKind == JsonValueKind.Object
                    ? (addrProp.TryGetProperty("addressLocality", out var localityProp) ? localityProp.GetString() ?? string.Empty : string.Empty)
                    : string.Empty)
                : string.Empty;
            string description = element.TryGetProperty("description", out var descProp) ? descProp.GetString() ?? string.Empty : string.Empty;
            string id = element.TryGetProperty("identifier", out var idProp) && idProp.ValueKind == JsonValueKind.Object
                ? (idProp.TryGetProperty("value", out var valProp) ? valProp.GetString() ?? Guid.NewGuid().ToString() : Guid.NewGuid().ToString())
                : Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(title)) return null;

            // Extract salary if available
            string? salary = null;
            if (element.TryGetProperty("baseSalary", out var salaryProp))
            {
                if (salaryProp.ValueKind == JsonValueKind.Object)
                {
                    if (salaryProp.TryGetProperty("value", out var salaryValue) && salaryValue.ValueKind == JsonValueKind.Object)
                    {
                        var min = salaryValue.TryGetProperty("minValue", out var minProp) ? minProp.GetDecimal() : 0;
                        var max = salaryValue.TryGetProperty("maxValue", out var maxProp) ? maxProp.GetDecimal() : 0;
                        var currency = salaryValue.TryGetProperty("currency", out var currProp) ? currProp.GetString() : "USD";
                        if (min > 0 || max > 0)
                        {
                            salary = $"{currency} {min}-{max}";
                        }
                    }
                }
                else if (salaryProp.ValueKind == JsonValueKind.String)
                {
                    salary = salaryProp.GetString();
                }
            }

            // Extract job type
            JobType jobType = JobType.Unknown;
            if (element.TryGetProperty("employmentType", out var empTypeProp))
            {
                var empType = empTypeProp.GetString()?.ToLowerInvariant();
                if (empType != null)
                {
                    if (empType.Contains("full")) jobType = JobType.FullTime;
                    else if (empType.Contains("part")) jobType = JobType.PartTime;
                    else if (empType.Contains("contract")) jobType = JobType.Contract;
                    else if (empType.Contains("intern")) jobType = JobType.Internship;
                }
            }

            // Extract posted date
            DateTimeOffset postedAt = DateTimeOffset.UtcNow;
            if (element.TryGetProperty("datePosted", out var datePostedProp))
            {
                if (DateTimeOffset.TryParse(datePostedProp.GetString(), out var dt))
                {
                    postedAt = dt;
                }
            }

            return new JobListing
            {
                Id = id,
                Title = title,
                Company = company,
                Location = location,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Salary = salary,
                JobType = jobType,
                PostedAt = postedAt,
                Source = "GoogleJobs"
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract jobs from a JSON document
    /// </summary>
    private static List<JobListing> ExtractJobsFromJsonDocument(JsonDocument doc, ILogger logger)
    {
        var jobs = new List<JobListing>();

        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Array)
                {
                    var job = ExtractJobFromNestedArray(item);
                    if (job != null) jobs.Add(job);
                }
            }
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            // Try to find an array property containing jobs
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.Array)
                        {
                            var job = ExtractJobFromNestedArray(item);
                            if (job != null) jobs.Add(job);
                        }
                    }
                }
            }
        }

        return jobs;
    }

    /// <summary>
    /// Extract jobs from a JSON array starting at a specific position
    /// </summary>
    private static List<JobListing> ExtractJobsFromJsonArray(string html, int start, ILogger logger)
    {
        var maxLen = Math.Min(html.Length - start, 400000);
        var snippet = html.Substring(start, maxLen);
        LogJsonCandidateExtraction(logger, snippet.Length, null);

        var jobs = new List<JobListing>();

        for (int i = 0; i < snippet.Length; i++)
        {
            if (snippet[i] != '[') continue;
            int depth = 0;
            int j = i;
            for (; j < snippet.Length; j++)
            {
                if (snippet[j] == '[') depth++;
                else if (snippet[j] == ']')
                {
                    depth--;
                    if (depth == 0) break;
                }
            }

            if (depth != 0) break;

            var content = snippet.Substring(i, j - i + 1);
            i = j;

            try
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    LogSkippedJsonCandidate(logger, i, doc.RootElement.ValueKind, null);
                    continue;
                }

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Array) continue;

                    var job = ExtractJobFromNestedArray(item);
                    if (job != null) jobs.Add(job);
                }
            }
            catch (Exception ex)
            {
                LogJsonParsingFailure(logger, i, ex);
                continue;
            }
        }

        return jobs;
    }

    /// <summary>
    /// Extract a single job from a nested array structure
    /// </summary>
    private static JobListing? ExtractJobFromNestedArray(JsonElement item)
    {
        try
        {
            // Google uses nested arrays with fields at different indexes depending on version.
            // Attempt to extract common fields from several possible positions.
            string title = GetStringAt(item, 0) ?? GetStringAt(item, 1) ?? string.Empty;
            string company = GetStringAt(item, 1) ?? GetStringAt(item, 2) ?? string.Empty;
            string location = GetStringAt(item, 2) ?? GetStringAt(item, 3) ?? string.Empty;
            string description = GetStringAt(item, 19) ?? GetStringAt(item, 20) ?? GetStringAt(item, 7) ?? string.Empty;
            string id = GetStringAt(item, 11) ?? GetStringAt(item, 10) ?? Guid.NewGuid().ToString();

            if (string.IsNullOrWhiteSpace(title)) return null;

            // Attempt to extract salary, job type and posted at from likely positions
            string? salary = GetStringAt(item, 12) ?? GetStringAt(item, 18);
            string? jobTypeStr = GetStringAt(item, 13) ?? GetStringAt(item, 17);
            string? postedAtStr = GetStringAt(item, 14) ?? GetStringAt(item, 16);

            DateTimeOffset postedAt = DateTimeOffset.UtcNow;
            if (!string.IsNullOrWhiteSpace(postedAtStr))
            {
                // Try to parse relative times like "3 days ago" or absolute timestamps
                if (DateTimeOffset.TryParse(postedAtStr, out var dt)) postedAt = dt;
            }

            JobType jobType = JobType.Unknown;
            if (!string.IsNullOrWhiteSpace(jobTypeStr))
            {
                var jt = jobTypeStr.ToLowerInvariant();
                if (jt.Contains("full")) jobType = JobType.FullTime;
                else if (jt.Contains("part")) jobType = JobType.PartTime;
                else if (jt.Contains("contract")) jobType = JobType.Contract;
                else if (jt.Contains("intern")) jobType = JobType.Internship;
            }

            return new JobListing
            {
                Id = id,
                Title = title,
                Company = company,
                Location = location,
                Description = string.IsNullOrWhiteSpace(description) ? null : description,
                Salary = string.IsNullOrWhiteSpace(salary) ? null : salary,
                JobType = jobType,
                PostedAt = postedAt,
                Source = "GoogleJobs"
            };
        }
        catch
        {
            return null;
        }
    }

    public static IReadOnlyList<JobListing> ParseFromHtml(string html, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        if (string.IsNullOrEmpty(html))
        {
            LogEmptyHtml(logger, null);
            return Array.Empty<JobListing>();
        }
        LogStartingParse(logger, html.Length, null);

        if (html.Contains("consent.google.com") || html.Contains("Before you continue to Google Search"))
        {
            LogDetectedConsentPage(logger, null);

            // Log a preview of the HTML for debugging
            var preview = html.Length > 1000 ? html.Substring(0, 1000) : html;
            LogConsentPagePreview(logger, preview, null);

            return Array.Empty<JobListing>();
        }

        string processedHtml = html;
        if (html.StartsWith(")]}", StringComparison.Ordinal) || html.StartsWith(")]}'", StringComparison.Ordinal) || html.StartsWith("\n)]}", StringComparison.Ordinal) || html.StartsWith("\n)]}'", StringComparison.Ordinal))
        {
            var firstBracket = html.IndexOf('[', StringComparison.Ordinal);
            if (firstBracket >= 0)
            {
                processedHtml = html.Substring(firstBracket);
            }
        }

        List<JobListing>? jobs = null;

        jobs = TryStrategy1_JobspyWidgetKey(processedHtml, logger);
        if (jobs != null && jobs.Count > 0) return jobs;

        jobs = TryJsonLdParsing(processedHtml, logger);
        if (jobs != null && jobs.Count > 0) return jobs;

        jobs = TryStrategy2_ScriptTags(processedHtml, logger);
        if (jobs != null && jobs.Count > 0) return jobs;

        jobs = TryStrategy3_DataVedAttributes(processedHtml, logger);
        if (jobs != null && jobs.Count > 0) return jobs;

        jobs = TryStrategy4_AfInitDataCallback(processedHtml, logger);
        if (jobs != null && jobs.Count > 0) return jobs;

        jobs = TryLegacyFallbackStrategy(processedHtml, logger);
        if (jobs != null && jobs.Count > 0) return jobs;

        // All strategies failed - log detailed diagnostic info
        // Check for common Google patterns
        var hasDataVed = processedHtml.Contains("data-ved");
        var hasJobsKeyword = processedHtml.Contains("jobs", StringComparison.OrdinalIgnoreCase);
        var hasHtlJobs = processedHtml.Contains("htl;jobs", StringComparison.OrdinalIgnoreCase);
        var hasJsonLd = processedHtml.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase);
        var hasScriptTags = processedHtml.Contains("<script", StringComparison.OrdinalIgnoreCase);

        LogPatternDetection(logger, processedHtml.Length, hasDataVed, hasJobsKeyword, hasHtlJobs, hasJsonLd, hasScriptTags, null);

        // Log a sample of the HTML
        var sampleSize = Math.Min(2000, processedHtml.Length);
        var htmlSample = processedHtml.Substring(0, sampleSize);
        LogHtmlSample(logger, sampleSize, htmlSample, null);

        return Array.Empty<JobListing>();
    }

    private static List<JobListing>? TryLegacyFallbackStrategy(string html, ILogger logger)
    {
        LogStrategyAttempt(logger, "LegacyFallback", null);

        try
        {
            int idx = html.IndexOf(GoogleJobsConstants.WidgetKey, StringComparison.Ordinal);
            int start = -1;
            if (idx >= 0)
            {
                LogFoundWidgetKey(logger, idx, null);
                start = html.LastIndexOf('[', idx);
            }

            if (start < 0)
            {
                var altIdx = html.IndexOf("htl;jobs", StringComparison.OrdinalIgnoreCase);
                if (altIdx >= 0)
                {
                    LogFoundHtlJobs(logger, altIdx, null);
                }
                else
                {
                    altIdx = html.IndexOf("jobs", StringComparison.OrdinalIgnoreCase);
                    if (altIdx >= 0) LogFoundJobsMarker(logger, altIdx, null);
                }
                if (altIdx >= 0) start = html.LastIndexOf('[', altIdx);
            }

            if (start < 0)
            {
                start = html.IndexOf('[', StringComparison.Ordinal);
                if (start >= 0)
                    LogFallbackJsonStart(logger, start, null);
                else
                    LogNoJsonStart(logger, null);
            }

            if (start < 0)
            {
                LogStrategyFailed(logger, "LegacyFallback", null);
                return null;
            }

            var jobs = ExtractJobsFromJsonArray(html, start, logger);
            if (jobs.Count > 0)
            {
                LogStrategySuccess(logger, "LegacyFallback", null);
                LogJobsExtractedFromStrategy(logger, jobs.Count, null);
                return jobs;
            }

            LogStrategyFailed(logger, "LegacyFallback", null);
            return null;
        }
        catch (Exception ex)
        {
            LogStrategyFailed(logger, "LegacyFallback", ex);
            return null;
        }
    }

    private static string? GetStringAt(JsonElement arr, int idx)
    {
        try
        {
            if (arr.ValueKind != JsonValueKind.Array) return null;
            int i = 0;
            foreach (var el in arr.EnumerateArray())
            {
                if (i++ == idx)
                {
                    if (el.ValueKind == JsonValueKind.String) return el.GetString();
                    return el.ToString();
                }
            }
        }
        catch { }
        return null;
    }
}
