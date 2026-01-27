using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ghostwright.Contracts.Jobs;

namespace Ghostwright.Platform.LinkedIn.Internal;

internal static class JsonLdParser
{
    private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

    public static JobListing? Parse(string html, string jobId, string url)
    {
        try
        {
            var match = Regex.Match(html, "<script type=\"application/ld\\+json\">(.*?)</script>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            if (!match.Success) return null;

            var json = match.Groups[1].Value.Trim();

            var ld = JsonSerializer.Deserialize<LinkedInJobPostingLd>(json, _jsonOptions);
            if (ld == null) return null;

            var location = ld.JobLocation?.Address?.AddressLocality ?? ld.JobLocation?.Address?.AddressRegion;

            DateTimeOffset posted = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(ld.DatePosted) && DateTimeOffset.TryParse(ld.DatePosted, out var parsed))
            {
                posted = parsed;
            }

            return new JobListing
            {
                Id = string.IsNullOrEmpty(jobId) ? ExtractIdFromUrl(url) ?? string.Empty : jobId,
                Title = ld.Title ?? string.Empty,
                Company = ld.HiringOrganization?.Name ?? string.Empty,
                Location = location,
                Description = ld.Description,
                Salary = FormatSalary(ld.BaseSalary),
                PostedAt = posted,
                Url = url
            };
        }
        catch
        {
            return null;
        }
    }

    private static string? ExtractIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        // try to find digits at end like /jobs/view/123456789
        var m = Regex.Match(url, @"/jobs/(?:view|r)/(?<id>[0-9]+)", RegexOptions.IgnoreCase);
        if (m.Success) return m.Groups["id"].Value;

        // fallback: jobId query param
        var q = new UriBuilder(url);
        var query = System.Web.HttpUtility.ParseQueryString(q.Query);
        var id = query["jobId"] ?? query["id"];
        return id;
    }

    private static string? FormatSalary(BaseSalaryLd? salary)
    {
        if (salary?.Value == null) return null;

        var val = salary.Value;
        if (val.Value != null) return $"{val.Value} {salary.Currency}";
        if (val.MinValue != null && val.MaxValue != null) return $"{val.MinValue}-{val.MaxValue} {salary.Currency}";
        if (val.MinValue != null) return $"> {val.MinValue} {salary.Currency}";
        if (val.MaxValue != null) return $"< {val.MaxValue} {salary.Currency}";

        return null;
    }

    private sealed class LinkedInJobPostingLd
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? DatePosted { get; set; }
        public HiringOrganizationLd? HiringOrganization { get; set; }
        public JobLocationLd? JobLocation { get; set; }
        public BaseSalaryLd? BaseSalary { get; set; }
    }

    private sealed class HiringOrganizationLd
    {
        public string? Name { get; set; }
    }

    private sealed class JobLocationLd
    {
        public AddressLd? Address { get; set; }
    }

    private sealed class AddressLd
    {
        public string? AddressLocality { get; set; }
        public string? AddressRegion { get; set; }
    }

    private sealed class BaseSalaryLd
    {
        public string? Currency { get; set; }
        public SalaryValueLd? Value { get; set; }
    }

    private sealed class SalaryValueLd
    {
        [JsonPropertyName("value")]
        public double? Value { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
    }
}
