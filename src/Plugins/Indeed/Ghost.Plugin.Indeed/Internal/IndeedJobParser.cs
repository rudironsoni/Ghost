using System.Collections.Generic;
using System.Text.Json;
using Ghost.Contracts.Jobs;

namespace Ghost.Plugin.Indeed.Internal;

public static class IndeedJobParser
{
    public static IEnumerable<JobListing> ParseJobs(JsonElement root, string baseUrl = "https://www.indeed.com")
    {
        if (!root.TryGetProperty("data", out JsonElement data)) yield break;
        if (!data.TryGetProperty("jobSearch", out JsonElement jobSearch)) yield break;
        if (!jobSearch.TryGetProperty("results", out JsonElement results)) yield break;

        foreach (JsonElement item in results.EnumerateArray())
        {
            JsonElement job = item;
            if (item.TryGetProperty("job", out JsonElement nestedJob))
            {
                job = nestedJob;
            }

            string id = job.TryGetProperty("key", out JsonElement keyEl) ? keyEl.GetString() ?? string.Empty :
                     job.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
            string title = job.TryGetProperty("title", out JsonElement t) ? t.GetString() ?? string.Empty : string.Empty;
            string company = job.TryGetProperty("employer", out JsonElement e) && e.TryGetProperty("name", out JsonElement en) ? en.GetString() ?? string.Empty : string.Empty;
            string location = job.TryGetProperty("location", out JsonElement l) && l.TryGetProperty("formatted", out JsonElement f) && f.TryGetProperty("long", out JsonElement lon) ? lon.GetString() ?? string.Empty : string.Empty;
            string descriptionHtml = job.TryGetProperty("description", out JsonElement d) && d.TryGetProperty("html", out JsonElement dh) ? dh.GetString() ?? string.Empty : string.Empty;
            string description = HtmlSanitizer.StripHtmlTags(descriptionHtml);

            string salary = ExtractSalary(job);

            string url = $"{baseUrl}/viewjob?jk={id}";

            yield return new JobListing
            {
                Id = id,
                Title = title,
                Company = company,
                Location = location,
                Description = description,
                Salary = salary,
                Url = url,
                Source = "Indeed"
            };
        }
    }

    private static string ExtractSalary(JsonElement job)
    {
        if (!job.TryGetProperty("compensation", out JsonElement comp) ||
            !comp.TryGetProperty("baseSalary", out JsonElement baseS) ||
            baseS.ValueKind == JsonValueKind.Null)
            return string.Empty;

        if (baseS.TryGetProperty("range", out JsonElement range))
        {
            decimal min = range.TryGetProperty("min", out JsonElement minEl) ? minEl.GetDecimal() : 0;
            decimal max = range.TryGetProperty("max", out JsonElement maxEl) ? maxEl.GetDecimal() : 0;
            string currency = range.TryGetProperty("currency", out JsonElement cur) ? cur.GetString() ?? string.Empty : string.Empty;

            if (min > 0 && max > 0)
                return $"${min} - ${max} {currency}".Trim();
            else if (min > 0)
                return $"${min}+ {currency}".Trim();
            else if (max > 0)
                return $"Up to ${max} {currency}".Trim();
        }
        else if (baseS.TryGetProperty("value", out JsonElement valEl) && valEl.ValueKind == JsonValueKind.Number)
        {
            decimal value = valEl.GetDecimal();
            string currency = baseS.TryGetProperty("currency", out JsonElement cur) ? cur.GetString() ?? string.Empty : string.Empty;
            if (value > 0)
                return $"${value} {currency}".Trim();
        }

        return string.Empty;
    }
}
