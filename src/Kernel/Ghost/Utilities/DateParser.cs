using System.Globalization;

namespace Ghost.Utilities;

public class DateParser : IDateParser
{
    private static readonly string[] Formats = new[] { "MMM yyyy", "MMMM yyyy", "yyyy" };
    private readonly TimeProvider _timeProvider;

    public DateParser(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public DateOnly? ParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        if (DateOnly.TryParse(input, out DateOnly d))
            return d;

        if (DateTime.TryParse(input, out DateTime dt))
            return DateOnly.FromDateTime(dt);

        if (DateOnly.TryParseExact(input, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly ex))
            return ex;

        // Try Month Year like "Jan 2024"
        if (DateTime.TryParseExact(input, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime dt2))
            return DateOnly.FromDateTime(dt2);

        return null;
    }

    public (DateOnly? Start, DateOnly? End) ParseDateRange(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, null);

        input = input.Trim();

        // examples: "Jan 2024 - Present", "Mar 2020 - Jul 2021"
        string[] parts = input.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            DateOnly? single = ParseDate(parts[0]);
            // Single-date ranges should have no end (ongoing/single point -> End = null)
            return (single, null);
        }

        DateOnly? start = ParseDate(parts[0]);
        string? right = parts[1];
        // Treat "Present", "Now", "Current", "Today" (and variants) as ongoing/no end date
        string cleanedRight = System.Text.RegularExpressions.Regex.Replace(right ?? string.Empty, "[^A-Za-z]", "").ToLowerInvariant();
        if (cleanedRight == "present" || cleanedRight == "now" || cleanedRight == "current" || cleanedRight == "today")
        {
            return (start, null);
        }

        DateOnly? end = ParseDate(right);
        return (start, end);
    }

    public DateTime? ParseRelativeDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        string s = input.Trim();
        if (string.Equals(s, "today", StringComparison.OrdinalIgnoreCase))
            return _timeProvider.GetUtcNow().UtcDateTime.Date;

        if (s.EndsWith("ago", StringComparison.OrdinalIgnoreCase))
        {
            // e.g., "3 days ago"
            string[] parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && int.TryParse(parts[0], out int num))
            {
                string unit = parts[1].ToLowerInvariant();
                DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
                return unit switch
                {
                    "day" or "days" => now.AddDays(-num),
                    "hour" or "hours" => now.AddHours(-num),
                    "minute" or "minutes" => now.AddMinutes(-num),
                    "month" or "months" => now.AddMonths(-num),
                    "year" or "years" => now.AddYears(-num),
                    _ => null
                };
            }
        }

        // fallback
        if (DateTime.TryParse(s, out DateTime dt))
            return dt;

        return null;
    }
}
