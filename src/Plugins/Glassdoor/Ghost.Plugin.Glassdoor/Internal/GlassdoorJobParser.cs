using System.Globalization;
using System.Text.Json;
using Ghost.Contracts.Jobs;

namespace Ghost.Plugin.Glassdoor.Internal;

public static class GlassdoorJobParser
{
    public static IReadOnlyList<JobListing> ParseSearchResponse(string? json)
    {
        if (string.IsNullOrEmpty(json)) return Array.Empty<JobListing>();

        try
        {
            using var doc = JsonDocument.Parse(json);
            List<JobListing> jobs = [];

            // Recursively search for job arrays in the JSON structure
            FindJobArrays(doc.RootElement, jobs);

            return jobs;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            return Array.Empty<JobListing>();
        }
    }

    private static void FindJobArrays(JsonElement element, List<JobListing> jobs)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                // Try to parse as job first
                JobListing? jl = ParseJobItem(item);
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
            foreach (JsonProperty prop in element.EnumerateObject())
            {
                // Check if this property contains job data
                if (prop.Name.Contains("job", StringComparison.OrdinalIgnoreCase) &&
                    prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement item in prop.Value.EnumerateArray())
                    {
                        JobListing? jl = ParseJobItem(item);
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
            if (item.TryGetProperty("jobview", out JsonElement jobview) && jobview.ValueKind == JsonValueKind.Object)
            {
                if (jobview.TryGetProperty("header", out JsonElement header) && header.ValueKind == JsonValueKind.Object)
                {
                    title = GetString(header, "jobTitleText");
                    location = GetString(header, "locationName");
                    url = GetString(header, "jobLink");

                    if (header.TryGetProperty("employer", out JsonElement employer) && employer.ValueKind == JsonValueKind.Object)
                    {
                        company = GetString(employer, "name");
                    }

                    if (header.TryGetProperty("payPeriodAdjustedPay", out JsonElement pay) && pay.ValueKind == JsonValueKind.Object)
                    {
                        double? p10 = GetNumber(pay, "p10");
                        double? p90 = GetNumber(pay, "p90");
                        string? cur = GetString(pay, "payCurrency") ?? GetString(pay, "currency");
                        if (p10.HasValue || p90.HasValue)
                        {
                            string left = p10?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                            string right = p90?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
                            string range = p10.HasValue && p90.HasValue ? $"{left} - {right}" : (p10.HasValue ? left : right);
                            salary = cur is not null ? $"{range} {cur}" : range;
                        }
                    }
                }

                if (jobview.TryGetProperty("job", out JsonElement job) && job.ValueKind == JsonValueKind.Object)
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

            // Generate deterministic ID if not present
            id ??= GlassdoorIdGenerator.GenerateDeterministicId(title, company, location, url);

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
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}"); return null; }
    }

    private static string? GetString(JsonElement el, params string[] names)
    {
        foreach (string name in names)
        {
            try
            {
                if (el.TryGetProperty(name, out JsonElement v))
                {
                    if (v.ValueKind == JsonValueKind.String)
                    {
                        string? str = v.GetString();
                        if (!string.IsNullOrWhiteSpace(str))
                            return str;
                    }
                    else if (v.ValueKind != JsonValueKind.Null)
                    {
                        return v.ToString();
                    }
                }
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}"); }
        }
        return null;
    }

    private static double? GetNumber(JsonElement el, string name)
    {
        try
        {
            if (!el.TryGetProperty(name, out JsonElement v)) return null;
            if (v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out double d)) return d;
            if (v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), out d)) return d;
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}"); }
        return null;
    }
}
