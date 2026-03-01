using System.Security.Cryptography;
using System.Text;
using Ghost.Sdk.Spider.Adapters.Contracts;

namespace Ghost.Sdk.Deduplication;

/// <summary>
/// Provides methods for creating unique fingerprints from HTTP requests.
/// </summary>
/// <remarks>
/// Request fingerprinting is used for deduplication by creating a canonical
/// representation of a request. The fingerprint is based on the normalized URL,
/// HTTP method, and optionally the request body.
/// </remarks>
public static class RequestFingerprinter
{
    /// <summary>
    /// Creates a unique fingerprint for the given request.
    /// </summary>
    /// <param name="request">The request to fingerprint.</param>
    /// <returns>A SHA256 hash representing the request's unique fingerprint.</returns>
    /// <remarks>
    /// The fingerprint is generated from:
    /// <list type="bullet">
    /// <item>Normalized URL (fragments removed, query parameters sorted)</item>
    /// <item>HTTP method (uppercase)</item>
    /// <item>Request body (if present)</item>
    /// </list>
    /// </remarks>
    public static string CreateFingerprint(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string normalizedUrl = NormalizeUrl(request.Url);
        string method = request.Method.ToUpperInvariant();
        string body = request.Body ?? string.Empty;

        // Combine components for fingerprinting
        string fingerprintData = $"{method}:{normalizedUrl}:{body}";

        // Create SHA256 hash
        byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintData));

        // Convert to hex string
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Normalizes a URL for consistent fingerprinting.
    /// </summary>
    /// <param name="url">The URL to normalize.</param>
    /// <returns>A normalized URL string.</returns>
    /// <remarks>
    /// Normalization includes:
    /// <list type="bullet">
    /// <item>Removing URL fragments (#anchor)</item>
    /// <item>Sorting query parameters alphabetically</item>
    /// <item>Converting scheme and host to lowercase</item>
    /// <item>Removing default ports (80 for http, 443 for https)</item>
    /// </list>
    /// </remarks>
    internal static string NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return string.Empty;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
        {
            // If not a valid absolute URI, return as-is
            return url;
        }

        // Build normalized URL components
        string scheme = uri.Scheme.ToLowerInvariant();
        string host = uri.Host.ToLowerInvariant();
        string port = GetNormalizedPort(uri);
        string path = uri.AbsolutePath;

        // Sort query parameters
        string query = SortQueryParameters(uri.Query);

        // Build normalized URL (without fragment)
        string normalizedUrl = $"{scheme}://{host}{port}{path}{query}";

        return normalizedUrl;
    }

    /// <summary>
    /// Gets the normalized port string for a URI.
    /// </summary>
    /// <remarks>
    /// Default ports (80 for HTTP, 443 for HTTPS) are omitted.
    /// </remarks>
    private static string GetNormalizedPort(Uri uri)
    {
        bool isDefaultPort = (uri.Scheme == "http" && uri.Port == 80) ||
                           (uri.Scheme == "https" && uri.Port == 443);

        return isDefaultPort ? string.Empty : $":{uri.Port}";
    }

    /// <summary>
    /// Sorts query parameters alphabetically for consistent fingerprinting.
    /// </summary>
    /// <param name="query">The query string (including leading '?').</param>
    /// <returns>A sorted query string, or empty string if no parameters.</returns>
    private static string SortQueryParameters(string query)
    {
        if (string.IsNullOrEmpty(query) || query == "?")
        {
            return string.Empty;
        }

        // Remove leading '?'
        string queryWithoutPrefix = query.TrimStart('?');

        // Parse and sort parameters
        string[] parameters = queryWithoutPrefix
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToArray();

        if (parameters.Length == 0)
        {
            return string.Empty;
        }

        return "?" + string.Join("&", parameters);
    }
}
