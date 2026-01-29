using System;
using System.Collections.Generic;
using System.Text.Json;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.Google.Jobs.Internal;

internal static class GoogleJobsParser
{
    // Parse job listings from an HTML payload by locating embedded JSON arrays
    public static IReadOnlyList<JobListing> ParseFromHtml(string html)
    {
        if (string.IsNullOrEmpty(html)) return Array.Empty<JobListing>();

        var idx = html.IndexOf(GoogleJobsConstants.WidgetKey, StringComparison.Ordinal);
        if (idx < 0) return Array.Empty<JobListing>();

        var start = html.LastIndexOf('[', idx);
        if (start < 0) return Array.Empty<JobListing>();

        var maxLen = Math.Min(html.Length - start, 200000);
        var snippet = html.Substring(start, maxLen);

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
                if (doc.RootElement.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Array) continue;

                    string title = GetStringAt(item, 0) ?? string.Empty;
                    string company = GetStringAt(item, 1) ?? string.Empty;
                    string location = GetStringAt(item, 2) ?? string.Empty;
                    string description = GetStringAt(item, 19) ?? string.Empty;
                    string id = GetStringAt(item, 11) ?? Guid.NewGuid().ToString();

                    if (string.IsNullOrWhiteSpace(title)) continue;

                    jobs.Add(new JobListing
                    {
                        Id = id,
                        Title = title,
                        Company = company,
                        Location = location,
                        Description = description
                    });
                }
            }
            catch { }
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
