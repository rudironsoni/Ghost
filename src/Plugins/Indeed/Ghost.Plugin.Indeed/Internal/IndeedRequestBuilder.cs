using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace Ghost.Plugin.Indeed.Internal;

/// <summary>
/// Builds Indeed API requests.
/// Single responsibility: Request construction.
/// </summary>
public sealed class IndeedRequestBuilder
{
    private readonly string _apiKey;
    private readonly string _apiEndpoint;
    private readonly IReadOnlyDictionary<string, string> _baseHeaders;
    private readonly string? _contentTypeHeader;

    private static readonly HashSet<string> ContentHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "content-type"
    };

    public IndeedRequestBuilder(
        CountryCode country,
        string apiKey,
        string apiEndpoint,
        IReadOnlyDictionary<string, string> baseHeaders,
        string? contentTypeHeader)
    {
        _apiKey = apiKey;
        _apiEndpoint = apiEndpoint;
        _baseHeaders = baseHeaders;
        _contentTypeHeader = contentTypeHeader;
    }

    /// <summary>
    /// Builds the base headers for Indeed requests.
    /// </summary>
    public static Dictionary<string, string> BuildBaseHeaders(CountryCode country, string apiKey, out string? contentTypeHeader)
    {
        Dictionary<string, string> headers = IndeedConstants.GetHeaders(country, apiKey);

        contentTypeHeader = null;
        foreach (KeyValuePair<string, string> kv in headers)
        {
            if (ContentHeaderNames.Contains(kv.Key))
            {
                contentTypeHeader = kv.Value;
                break;
            }
        }

        return headers;
    }

    /// <summary>
    /// Creates a search request with the specified query and parameters.
    /// </summary>
    public HttpRequestMessage CreateSearchRequest(string query, string location, int limit, string? cursor)
    {
        string formattedQuery = BuildSearchQuery(query, location, limit, cursor);
        var payload = new { query = formattedQuery };

        return CreateRequest(payload);
    }

    /// <summary>
    /// Creates an HTTP request message with the specified payload.
    /// </summary>
    public HttpRequestMessage CreateRequest(object payload)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, _apiEndpoint)
        {
            Content = JsonContent.Create(payload)
        };

        if (req.Content != null)
        {
            if (!req.Content.Headers.Contains("Content-Type"))
            {
                if (!string.IsNullOrEmpty(_contentTypeHeader))
                {
                    req.Content.Headers.TryAddWithoutValidation("Content-Type", _contentTypeHeader);
                }
                else
                {
                    req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                }
            }
        }

        foreach (KeyValuePair<string, string> kv in _baseHeaders)
        {
            if (ContentHeaderNames.Contains(kv.Key))
            {
                continue;
            }

            req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }

        return req;
    }

    /// <summary>
    /// Builds a search query string with the specified parameters.
    /// </summary>
    public string BuildSearchQuery(string query, string location, int limit, string? cursor)
    {
        var sb = new StringBuilder();
        sb.Append($""""query: \"{EscapeGraphQLString(query)}\" location: \"{EscapeGraphQLString(location)}\" limit: {Math.Min(25, limit)}"""");

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            sb.Append($"""" after: \"{cursor}\""""");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Escapes special characters in a GraphQL string.
    /// </summary>
    private static string EscapeGraphQLString(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        return input
            .Replace("\\", "\\\\")
            .Replace(""""""", "\\\"""
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }

    /// <summary>
    /// Applies default headers to an HttpClient.
    /// </summary>
    public static void ApplyDefaultHeaders(HttpClient client, IReadOnlyDictionary<string, string> headers)
    {
        foreach (KeyValuePair<string, string> kv in headers)
        {
            if (ContentHeaderNames.Contains(kv.Key))
            {
                continue;
            }

            client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
        }
    }
}
