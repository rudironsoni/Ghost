using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            var job = item;
            if (item.TryGetProperty("job", out var nestedJob))
            {
                job = nestedJob;
            }

            var id = job.TryGetProperty("key", out var keyEl) ? keyEl.GetString() ?? string.Empty :
                     job.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? string.Empty : string.Empty;
            var title = job.TryGetProperty("title", out var t) ? t.GetString() ?? string.Empty : string.Empty;
            var company = job.TryGetProperty("employer", out var e) && e.TryGetProperty("name", out var en) ? en.GetString() ?? string.Empty : string.Empty;
            var location = job.TryGetProperty("location", out var l) && l.TryGetProperty("formatted", out var f) && f.TryGetProperty("long", out var lon) ? lon.GetString() ?? string.Empty : string.Empty;
            var descriptionHtml = job.TryGetProperty("description", out var d) && d.TryGetProperty("html", out var dh) ? dh.GetString() ?? string.Empty : string.Empty;
            var description = StripHtmlTags(descriptionHtml);

            string salary = ExtractSalary(job);

            var domain = "indeed.com";
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

    private static string StripHtmlTags(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return string.Empty;

        // Replace common block-level tags with newlines for better formatting
        html = html.Replace("<br>", "\n").Replace("<br/>", "\n").Replace("<br />", "\n");
        html = html.Replace("</p>", "\n\n").Replace("</div>", "\n");
        html = html.Replace("</li>", "\n").Replace("</h1>", "\n\n").Replace("</h2>", "\n\n").Replace("</h3>", "\n\n");

        // Remove all remaining HTML tags
        html = Regex.Replace(html, "<[^>]+>", string.Empty);

        // Decode common HTML entities
        html = html.Replace("&nbsp;", " ").Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">");
        html = html.Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&rsquo;", "'").Replace("&lsquo;", "'");
        html = html.Replace("&rdquo;", "\"").Replace("&ldquo;", "\"").Replace("&ndash;", "-").Replace("&mdash;", "—");
        html = html.Replace("&hellip;", "...").Replace("&bull;", "•").Replace("&trade;", "™").Replace("&copy;", "©");
        html = html.Replace("&reg;", "®").Replace("&euro;", "€").Replace("&pound;", "£").Replace("&yen;", "¥");

        html = Regex.Replace(html, "[ \t]+", " ");
        html = Regex.Replace(html, "\n{3,}", "\n\n");

        return html.Trim();
    }

    private static string ExtractSalary(JsonElement job)
    {
        if (!job.TryGetProperty("compensation", out var comp) ||
            !comp.TryGetProperty("baseSalary", out var baseS) ||
            baseS.ValueKind == JsonValueKind.Null)
            return string.Empty;

        if (baseS.TryGetProperty("range", out var range))
        {
            var min = range.TryGetProperty("min", out var minEl) ? minEl.GetDecimal() : 0;
            var max = range.TryGetProperty("max", out var maxEl) ? maxEl.GetDecimal() : 0;
            var currency = range.TryGetProperty("currency", out var cur) ? cur.GetString() ?? string.Empty : string.Empty;

            if (min > 0 && max > 0)
                return $"${min} - ${max} {currency}".Trim();
            else if (min > 0)
                return $"${min}+ {currency}".Trim();
            else if (max > 0)
                return $"Up to ${max} {currency}".Trim();
        }
        else if (baseS.TryGetProperty("value", out var valEl) && valEl.ValueKind == JsonValueKind.Number)
        {
            var value = valEl.GetDecimal();
            var currency = baseS.TryGetProperty("currency", out var cur) ? cur.GetString() ?? string.Empty : string.Empty;
            if (value > 0)
                return $"${value} {currency}".Trim();
        }

        return string.Empty;
    }
}
