using System;
using System.Collections.Specialized;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Ghost.Contracts.Jobs;
using Microsoft.Extensions.Logging;

namespace Ghost.Plugin.LinkedIn.Internal;

internal sealed class JsonLdParser
{
    private readonly IJsonLdExtractor _extractor;
    private readonly ILogger<JsonLdParser> _logger;

    public JsonLdParser(IJsonLdExtractor extractor, ILogger<JsonLdParser> logger)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        _extractor = extractor;
        _logger = logger;
    }

    public JobListing? Parse(string html, string jobId, string url)
    {
        try
        {
            IEnumerable<LinkedInJobPostingLd> ldEnum = _extractor.Extract<LinkedInJobPostingLd>(html);
            LinkedInJobPostingLd? ld = System.Linq.Enumerable.FirstOrDefault(ldEnum);
            if (ld == null) return null;

            string? location = ld.JobLocation?.Address?.AddressLocality ?? ld.JobLocation?.Address?.AddressRegion;

            DateTimeOffset posted = DateTimeOffset.UtcNow;
            if (!string.IsNullOrEmpty(ld.DatePosted) && DateTimeOffset.TryParse(ld.DatePosted, out DateTimeOffset parsed))
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
                Url = url,
                Source = "LinkedIn"
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
        // Normalize into an uppercase token string and be resilient to arrays or JSON shaped strings
        string s = type.ToUpperInvariant();

        // If it's an array or contains JSON quoting, extract the first token of letters/underscores
        if (s.Contains('[') || s.Contains('"') || s.Contains(','))
        {
            Match m = System.Text.RegularExpressions.Regex.Match(s, "[A-Z_]+\\b");
            if (m.Success) s = m.Value;
        }

        // Allow values like FULL_TIME, FULL-TIME, Full time, etc.
        string cleaned = System.Text.RegularExpressions.Regex.Replace(s, "[^A-Z]", "");

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
            var uriBuilder = new UriBuilder(url);
            NameValueCollection query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            string? id = query["jobId"] ?? query["id"];
            return id;
        }
        catch
        {
            return null;
        }
    }

    private static string? FormatSalary(BaseSalaryLd? salary)
    {
        if (salary is null) return null;

        // Currency may be present at top-level or inside the value object
        string? currency = salary.Currency;

        // Value is represented as a JsonElement because LinkedIn sometimes returns a simple
        // number or a complex QuantitativeValue object. Inspect and extract min/max/value.
        if (salary.Value.HasValue)
        {
            JsonElement el = salary.Value.Value;
            try
            {
                if (el.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    if (el.TryGetDouble(out double single))
                    {
                        return FormatAmount(single, currency);
                    }
                }
                else if (el.ValueKind == System.Text.Json.JsonValueKind.Object)
                {
                    // Try common property names
                    double? min = TryGetDouble(el, "minValue") ?? TryGetDouble(el, "minimumValue") ?? TryGetDoubleFromNested(el, "minValue");
                    double? max = TryGetDouble(el, "maxValue") ?? TryGetDouble(el, "maximumValue") ?? TryGetDoubleFromNested(el, "maxValue");
                    double? exact = TryGetDouble(el, "value") ?? TryGetDoubleFromNested(el, "value");

                    // currency might live inside the object as well
                    if (string.IsNullOrEmpty(currency) && el.TryGetProperty("currency", out JsonElement curEl) && curEl.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        currency = curEl.GetString();
                    }

                    if (exact.HasValue) return FormatAmount(exact.Value, currency);
                    if (min.HasValue && max.HasValue) return $"{min.Value}-{max.Value} {currency}".Trim();
                    if (min.HasValue) return $"> {min.Value} {currency}".Trim();
                    if (max.HasValue) return $"< {max.Value} {currency}".Trim();
                }
                else if (el.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    string? s = el.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) return s;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
            }
        }

        return null;

        static string? FormatAmount(double amount, string? cur)
        {
            if (string.IsNullOrWhiteSpace(cur)) return amount.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return $"{amount.ToString(System.Globalization.CultureInfo.InvariantCulture)} {cur}";
        }

        static double? TryGetDouble(System.Text.Json.JsonElement obj, string prop)
        {
            if (obj.TryGetProperty(prop, out JsonElement p))
            {
                if (p.ValueKind == System.Text.Json.JsonValueKind.Number && p.TryGetDouble(out double d)) return d;
                if (p.ValueKind == System.Text.Json.JsonValueKind.String && double.TryParse(p.GetString(), out double d2)) return d2;
            }
            return null;
        }

        // Some LinkedIn payloads nest the quantitative value under an inner "value" object
        static double? TryGetDoubleFromNested(System.Text.Json.JsonElement obj, string prop)
        {
            if (obj.TryGetProperty("value", out JsonElement inner) && inner.ValueKind == System.Text.Json.JsonValueKind.Object)
                return TryGetDouble(inner, prop);
            return null;
        }
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

        // The JSON-LD 'value' property can be a simple number or an object; capture raw JsonElement
        [JsonPropertyName("value")]
        public JsonElement? Value { get; set; }
    }
}
