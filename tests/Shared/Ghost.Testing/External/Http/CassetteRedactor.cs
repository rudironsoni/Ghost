using System.Linq;

namespace Ghost.Testing.External.Http;

public static class CassetteRedactor
{
    public const string RedactedValue = "[REDACTED]";

    private static readonly HashSet<string> SensitiveHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "X-Auth-Token"
    };

    private static readonly HashSet<string> SensitiveQueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "api_key",
        "apikey",
        "key",
        "token",
        "signature",
        "sig"
    };

    public static string RedactUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            return url;
        }

        return RedactUri(uri).AbsoluteUri;
    }

    public static Uri RedactUri(Uri uri)
    {
        IReadOnlyList<KeyValuePair<string, string>> redactedPairs = QueryStringUtilities
            .Parse(uri.Query)
            .Select(pair => SensitiveQueryKeys.Contains(pair.Key)
                ? new KeyValuePair<string, string>(pair.Key, RedactedValue)
                : pair)
            .ToList();

        UriBuilder builder = new(uri)
        {
            Fragment = string.Empty,
            Query = QueryStringUtilities.BuildNormalizedQuery(redactedPairs)
        };

        return builder.Uri;
    }

    public static Dictionary<string, List<string>> RedactHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers)
    {
        return headers
            .GroupBy(header => header.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => SensitiveHeaders.Contains(group.Key)
                    ? [RedactedValue]
                    : group
                        .SelectMany(header => header.Value)
                        .ToList(),
                StringComparer.OrdinalIgnoreCase);
    }
}
