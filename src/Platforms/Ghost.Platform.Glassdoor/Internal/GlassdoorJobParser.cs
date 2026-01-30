using System.Globalization;
using System.Text.Json;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.Glassdoor.Internal;

public static class GlassdoorJobParser
{
    public static IReadOnlyList<JobListing> ParseSearchResponse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return Array.Empty<JobListing>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            var jobs = new List<JobListing>();

            // Recursively search for job arrays in the JSON structure
            FindJobArrays(doc.RootElement, jobs);

            return jobs;
        }
        catch
        {
            return Array.Empty<JobListing>();
        }
    }

    private static void FindJobArrays(JsonElement element, List<JobListing> jobs)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var jl = ParseJobItem(item);
                if (jl != null) jobs.Add(jl);
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                // Check if this property contains job data
                if (prop.Name.Contains("job", StringComparison.OrdinalIgnoreCase) && 
                    prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                    {
                        var jl = ParseJobItem(item);
                        if (jl != null) jobs.Add(jl);
                    }
                }
                else
                {
                    // Recursively search nested objects
                    FindJobArrays(prop.Value, jobs);
                }
            }
        }
    }

    private static JobListing? ParseJobItem(JsonElement item)
    {
        try
        {
            // Common fields
            string title = GetString(item, "jobTitleText") ?? string.Empty;
            string company = GetString(item, "employerNameFromSearch") ?? GetString(item, "employerName") ?? string.Empty;
            string id = GetString(item, "jobId") ?? Guid.NewGuid().ToString();
            string? location = GetString(item, "location") ?? GetString(item, "jobLocationCity");

            string? salary = null;
            if (item.TryGetProperty("header", out var header) && header.ValueKind == JsonValueKind.Object)
            {
                if (header.TryGetProperty("payPeriodAdjustedPay", out var pay) && pay.ValueKind == JsonValueKind.Object)
                {
                    var p10 = GetNumber(pay, "p10");
                    var p90 = GetNumber(pay, "p90");
                    var cur = GetString(pay, "payCurrency") ?? GetString(pay, "currency");
                    if (p10.HasValue || p90.HasValue)
                    {
                        var left = p10?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                        var right = p90?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                        var range = p10.HasValue && p90.HasValue ? $"{left} - {right}" : (p10.HasValue ? left : right);
                        salary = cur is not null ? $"{range} {cur}" : range;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(title)) return null;

            return new JobListing
            {
                Id = id,
                Title = title,
                Company = company,
                Location = location,
                Salary = salary
                ,
                Source = "Glassdoor"
            };
        }
        catch { return null; }
    }

    private static string? GetString(JsonElement el, string name)
    {
        try
        {
            if (!el.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.String) return v.GetString();
            return v.ToString();
        }
        catch { return null; }
    }

    private static double? GetNumber(JsonElement el, string name)
    {
        try
        {
            if (!el.TryGetProperty(name, out var v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var d)) return d;
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out d)) return d;
        }
        catch { }
        return null;
    }
}
