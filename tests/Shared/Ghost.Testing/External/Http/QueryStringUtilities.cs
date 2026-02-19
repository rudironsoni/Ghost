using System.Linq;

namespace Ghost.Testing.External.Http;

internal static class QueryStringUtilities
{
    public static IReadOnlyList<KeyValuePair<string, string>> Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        string normalizedQuery = query.StartsWith('?') ? query[1..] : query;

        return normalizedQuery
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(segment => segment.Split('=', 2))
            .Select(parts => new KeyValuePair<string, string>(
                Uri.UnescapeDataString(parts[0]),
                parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty))
            .ToList();
    }

    public static string BuildNormalizedQuery(IEnumerable<KeyValuePair<string, string>> pairs)
    {
        return string.Join(
            "&",
            pairs.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
    }
}
