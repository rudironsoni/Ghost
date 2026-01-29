using System;
using System.Text.Json.Serialization;
using Ghost.Contracts.Jobs;
using Ghost.Abstractions;

namespace Ghost.Platform.LinkedIn.Internal;

internal sealed class JsonLdParser
{
    private readonly IJsonLdExtractor _extractor;

    public JsonLdParser(IJsonLdExtractor extractor)
    {
        _extractor = extractor ?? throw new ArgumentNullException(nameof(extractor));
    }

    public JobListing? Parse(string html, string jobId, string url)
    {
        try
        {
            var ldEnum = _extractor.Extract<LinkedInJobPostingLd>(html);
            var ld = System.Linq.Enumerable.FirstOrDefault(ldEnum);
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
                JobType = ParseJobType(ld.EmploymentType),
                PostedAt = posted,
                Url = url
            };
        }
        catch
        {
            return null;
        }
    }

    private static JobType ParseJobType(string? type)
    {
        if (string.IsNullOrEmpty(type)) return JobType.Unknown;
        var s = type.ToUpperInvariant();

        // If it's an array or some JSON-looking structure, try to pull the first token
        if (s.Contains('['))
        {
            var m = System.Text.RegularExpressions.Regex.Match(s, "[A-Z_]+/");
            // fallback - extract letters/underscores
            if (!m.Success)
            {
                var m2 = System.Text.RegularExpressions.Regex.Match(s, "[A-Z_]+\\b");
                if (m2.Success) s = m2.Value;
            }
            else
            {
                s = m.Value;
            }
        }

        // Simplify by removing non-letter characters
        var cleaned = System.Text.RegularExpressions.Regex.Replace(s, "[^A-Z]", "");

        if (cleaned.Contains("FULL") && cleaned.Contains("TIME")) return JobType.FullTime;
        if (cleaned.Contains("PART") && cleaned.Contains("TIME")) return JobType.PartTime;
        if (cleaned.Contains("CONTRACT")) return JobType.Contract;
        if (cleaned.Contains("TEMP") || cleaned.Contains("TEMPORARY")) return JobType.Contract;
        if (cleaned.Contains("INTERN")) return JobType.Internship;

        return JobType.Unknown;
    }

    private static string? ExtractIdFromUrl(string? url)
    {
        if (string.IsNullOrEmpty(url)) return null;
        try
        {
            // fallback: jobId query param
            var q = new UriBuilder(url);
            var query = System.Web.HttpUtility.ParseQueryString(q.Query);
            var id = query["jobId"] ?? query["id"];
            return id;
        }
        catch
        {
            return null;
        }
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
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        [JsonPropertyName("description")]
        public string? Description { get; set; }
        [JsonPropertyName("datePosted")]
        public string? DatePosted { get; set; }
        [JsonPropertyName("employmentType")]
        public string? EmploymentType { get; set; }
        [JsonPropertyName("hiringOrganization")]
        public HiringOrganizationLd? HiringOrganization { get; set; }
        [JsonPropertyName("jobLocation")]
        public JobLocationLd? JobLocation { get; set; }
        [JsonPropertyName("baseSalary")]
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
