using System.Globalization;
using Ghost.Abstractions;

namespace Ghost.Utilities;

public class DateParser : IDateParser
{
    private static readonly string[] Formats = new[] { "MMM yyyy", "MMMM yyyy", "yyyy" };

    public DateOnly? ParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        input = input.Trim();

        if (DateOnly.TryParse(input, out var d))
            return d;

        if (DateTime.TryParse(input, out var dt))
            return DateOnly.FromDateTime(dt);

        if (DateOnly.TryParseExact(input, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var ex))
            return ex;

        // Try Month Year like "Jan 2024"
        if (DateTime.TryParseExact(input, Formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var dt2))
            return DateOnly.FromDateTime(dt2);

        return null;
    }

    public (DateOnly? Start, DateOnly? End) ParseDateRange(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return (null, null);

        input = input.Trim();

        // examples: "Jan 2024 - Present", "Mar 2020 - Jul 2021"
        var parts = input.Split('-', StringSplitOptions.TrimEntries);
        if (parts.Length == 1)
        {
            var single = ParseDate(parts[0]);
            return (single, single);
        }

        var start = ParseDate(parts[0]);
        var right = parts[1];
        if (string.Equals(right, "Present", StringComparison.OrdinalIgnoreCase) || string.Equals(right, "Today", StringComparison.OrdinalIgnoreCase))
        {
            return (start, DateOnly.FromDateTime(DateTime.UtcNow));
        }

        var end = ParseDate(right);
        return (start, end);
    }

    public DateTime? ParseRelativeDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return null;

        var s = input.Trim();
        if (string.Equals(s, "today", StringComparison.OrdinalIgnoreCase))
            return DateTime.UtcNow.Date;

        if (s.EndsWith("ago", StringComparison.OrdinalIgnoreCase))
        {
            // e.g., "3 days ago"
            var parts = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && int.TryParse(parts[0], out var num))
            {
                var unit = parts[1].ToLowerInvariant();
                return unit switch
                {
                    "day" or "days" => DateTime.UtcNow.AddDays(-num),
                    "hour" or "hours" => DateTime.UtcNow.AddHours(-num),
                    "minute" or "minutes" => DateTime.UtcNow.AddMinutes(-num),
                    "month" or "months" => DateTime.UtcNow.AddMonths(-num),
                    "year" or "years" => DateTime.UtcNow.AddYears(-num),
                    _ => null
                };
            }
        }

        // fallback
        if (DateTime.TryParse(s, out var dt))
            return dt;

        return null;
    }
}
