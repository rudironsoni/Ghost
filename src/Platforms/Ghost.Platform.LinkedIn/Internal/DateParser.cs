using System;
using System.Globalization;

namespace Ghost.Platform.LinkedIn.Internal;

internal static class DateParser
{
    private static readonly string[] s_rangeSeparator = new[] { " - " };
    public static (DateTimeOffset? Start, DateTimeOffset? End) Parse(string dateRangeText)
    {
        if (string.IsNullOrWhiteSpace(dateRangeText)) return (null, null);

        var parts = dateRangeText.Split(s_rangeSeparator, StringSplitOptions.None);
        if (parts.Length == 1)
        {
            var start = ParseSingle(parts[0]);
            // if it's a single year, treat end as null
            return (start, null);
        }

        DateTimeOffset? s = ParseSingle(parts[0]);
        DateTimeOffset? e = null;
        var right = parts[1].Trim();
        if (!string.Equals(right, "Present", StringComparison.OrdinalIgnoreCase))
        {
            e = ParseSingle(right);
        }

        return (s, e);
    }

    private static DateTimeOffset? ParseSingle(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = text.Trim();

        // Try MMM yyyy like "Jan 2020"
        if (DateTimeOffset.TryParseExact(text, "MMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var dto))
            return dto;

        // Try full month name e.g., "January 2020"
        if (DateTimeOffset.TryParseExact(text, "MMMM yyyy", CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
            return dto;

        // Try year only
        if (int.TryParse(text, out var year) && year >= 1 && year <= 9999)
        {
            return new DateTimeOffset(new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        }

        // Fallback to generic parse
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dto))
            return dto;

        return null;
    }
}
