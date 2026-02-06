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
                // Try to parse as job first
                var jl = ParseJobItem(item);
                if (jl != null)
                {
                    jobs.Add(jl);
                }
                else if (item.ValueKind == JsonValueKind.Object)
                {
                    // If not a job, recursively search inside the object
                    FindJobArrays(item, jobs);
                }
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
            // Initialize with empty values
            string? title = null;
            string? company = null;
            string? id = null;
            string? location = null;
            string? salary = null;
            string? description = null;
            string? url = null;

            // Try to extract from nested jobview structure (from GraphQL response)
            if (item.TryGetProperty("jobview", out var jobview) && jobview.ValueKind == JsonValueKind.Object)
            {
                if (jobview.TryGetProperty("header", out var header) && header.ValueKind == JsonValueKind.Object)
                {
                    title = GetString(header, "jobTitleText");
                    location = GetString(header, "locationName");
                    url = GetString(header, "jobLink");
                    
                    if (header.TryGetProperty("employer", out var employer) && employer.ValueKind == JsonValueKind.Object)
                    {
                        company = GetString(employer, "name");
                    }

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
                
                if (jobview.TryGetProperty("job", out var job) && job.ValueKind == JsonValueKind.Object)
                {
                    id = GetString(job, "listingId");
                    description = GetString(job, "description");
                }
            }
            else
            {
                // Fallback: Try direct field access (for flat structures)
                title = GetString(item, "jobTitleText", "title", "jobTitle");
                company = GetString(item, "employerNameFromSearch", "employerName", "employer", "company");
                id = GetString(item, "jobId", "listingId", "id");
                location = GetString(item, "location", "jobLocationCity", "locationName");
                description = GetString(item, "description", "jobDescription");
                url = GetString(item, "jobLink", "link", "url");
            }

            // Must have at least a title
            if (string.IsNullOrWhiteSpace(title))
                return null;

            // Generate ID if not present
            id ??= Guid.NewGuid().ToString();

            return new JobListing
            {
                Id = id,
                Title = title,
                Company = company ?? "Unknown Company",
                Location = location,
                Salary = salary,
                Description = description,
                Url = url,
                Source = "Glassdoor"
            };
        }
        catch { return null; }
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            try
            {
                if (el.TryGetProperty(name, out var v))
                {
                    if (v.ValueKind == JsonValueKind.String)
                    {
                        var str = v.GetString();
                        if (!string.IsNullOrWhiteSpace(str))
                            return str;
                    }
                    else if (v.ValueKind != JsonValueKind.Null)
                    {
                        return v.ToString();
                    }
                }
            }
            catch { }
        }
        return null;
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
