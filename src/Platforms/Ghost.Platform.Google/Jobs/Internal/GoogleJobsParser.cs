using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.Google.Jobs.Internal;

internal static class GoogleJobsParser
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


    // Parse job listings from an HTML payload by locating embedded JSON arrays
    public static IReadOnlyList<JobListing> ParseFromHtml(string html, ILogger? logger = null)
    {
        logger ??= NullLogger.Instance;
        if (string.IsNullOrEmpty(html))
        {
            LogEmptyHtml(logger, null);
            return Array.Empty<JobListing>();
        }
        LogStartingParse(logger, html.Length, null);

        // The Jobs widget contains embedded JSON arrays; attempt several heuristics to locate them.
        // First try the known widget key marker. If not found, try to find common JSON prefixes
        // used by Google Jobs ("[", "[[[" near 'jobs' markers) and also support async callback payloads.

        int idx = html.IndexOf(GoogleJobsConstants.WidgetKey, StringComparison.Ordinal);
        int start = -1;
        if (idx >= 0)
        {
            LogFoundWidgetKey(logger, idx, null);
            start = html.LastIndexOf('[', idx);
        }

        // Fallback: look for 'htl;jobs' marker or 'jobs' text and take the nearest '[' before it
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

        // Final fallback: find the first large JSON array in the document
        if (start < 0)
        {
            start = html.IndexOf('[', StringComparison.Ordinal);
            if (start >= 0)
                LogFallbackJsonStart(logger, start, null);
            else
                LogNoJsonStart(logger, null);
        }

        if (start < 0) return Array.Empty<JobListing>();

        var maxLen = Math.Min(html.Length - start, 400000); // increase scan length for larger payloads
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

            if (depth != 0) break; // unbalanced - stop

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

                    // Google uses nested arrays with fields at different indexes depending on version.
                    // Attempt to extract common fields from several possible positions.
                    string title = GetStringAt(item, 0) ?? GetStringAt(item, 1) ?? string.Empty;
                    string company = GetStringAt(item, 1) ?? GetStringAt(item, 2) ?? string.Empty;
                    string location = GetStringAt(item, 2) ?? GetStringAt(item, 3) ?? string.Empty;
                    string description = GetStringAt(item, 19) ?? GetStringAt(item, 20) ?? GetStringAt(item, 7) ?? string.Empty;
                    string id = GetStringAt(item, 11) ?? GetStringAt(item, 10) ?? Guid.NewGuid().ToString();

                    if (string.IsNullOrWhiteSpace(title)) continue;

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

                    jobs.Add(new JobListing
                    {
                        Id = id,
                        Title = title,
                        Company = company,
                        Location = location,
                        Description = string.IsNullOrWhiteSpace(description) ? null : description,
                        Salary = string.IsNullOrWhiteSpace(salary) ? null : salary,
                        JobType = jobType,
                        PostedAt = postedAt,
                        Source = "Google"
                    });
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
