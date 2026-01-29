using System;
using System.Collections.Generic;
using System.Text.Json;
using Ghost.Contracts.Jobs;

namespace Ghost.Platform.Indeed.Internal;

public static class IndeedJobParser
{
    public static IEnumerable<JobListing> ParseJobs(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data)) yield break;
        if (!data.TryGetProperty("jobSearch", out var jobSearch)) yield break;
        if (!jobSearch.TryGetProperty("results", out var results)) yield break;

        foreach (var item in results.EnumerateArray())
        {
            var id = item.GetProperty("id").GetString() ?? string.Empty;
            var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var company = item.TryGetProperty("employer", out var e) && e.TryGetProperty("name", out var en) ? en.GetString() ?? string.Empty : string.Empty;
            var location = item.TryGetProperty("location", out var l) && l.TryGetProperty("formatted", out var f) && f.TryGetProperty("long", out var lon) ? lon.GetString() ?? string.Empty : string.Empty;
            var description = item.TryGetProperty("description", out var d) && d.TryGetProperty("html", out var dh) ? dh.GetString() ?? string.Empty : string.Empty;

            string salary = string.Empty;
            if (item.TryGetProperty("compensation", out var comp) && comp.TryGetProperty("baseSalary", out var baseS) && baseS.TryGetProperty("range", out var range))
            {
                var min = range.TryGetProperty("min", out var minEl) ? minEl.GetDecimal() : 0;
                var max = range.TryGetProperty("max", out var maxEl) ? maxEl.GetDecimal() : 0;
                var currency = range.TryGetProperty("currency", out var cur) ? cur.GetString() ?? string.Empty : string.Empty;
                salary = $"${min} - ${max} {currency}".Trim();
            }

            var domain = "indeed.com"; // default - more accurate mapping may be applied using headers
            var url = $"https://{domain}/viewjob?jk={id}";

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
}
