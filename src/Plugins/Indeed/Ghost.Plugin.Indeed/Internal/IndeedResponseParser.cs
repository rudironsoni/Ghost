using System.Text.Json;

namespace Ghost.Plugin.Indeed.Internal;

/// <summary>
/// Parses Indeed API responses.
/// Single responsibility: Response parsing.
/// </summary>
public sealed class IndeedResponseParser
{
    /// <summary>
    /// Extracts the next cursor from a search response for pagination.
    /// </summary>
    public string? ExtractNextCursor(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("data", out JsonElement data))
        {
            return null;
        }

        if (!data.TryGetProperty("jobSearch", out JsonElement jobSearch))
        {
            return null;
        }

        if (!jobSearch.TryGetProperty("pageInfo", out JsonElement pageInfo))
        {
            return null;
        }

        if (!pageInfo.TryGetProperty("nextCursor", out JsonElement nextCursorElement))
        {
            return null;
        }

        return nextCursorElement.GetString();
    }

    /// <summary>
    /// Checks if the response indicates a block or consent requirement.
    /// </summary>
    public bool IsBlockedOrConsentRequired(string responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
            return true;

        string trimmed = responseContent.TrimStart();

        // If it starts with valid JSON object with "data" property, it's likely a valid response
        if (trimmed.StartsWith("{\"data\":", StringComparison.Ordinal) ||
            trimmed.StartsWith("{\"data\": ", StringComparison.Ordinal))
        {
            return false;
        }

        // Check for explicit error page indicators
        return trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("\"<", StringComparison.Ordinal) ||
               responseContent.Contains("\"errors\":", StringComparison.Ordinal) ||
               responseContent.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("throttled", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("\"unauthorized\":", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("\"forbidden\":", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("g-recaptcha", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("cf_chl_jschl", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that the response contains job search data.
    /// </summary>
    public bool ContainsJobData(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("data", out JsonElement data))
        {
            return false;
        }

        if (!data.TryGetProperty("jobSearch", out JsonElement jobSearch))
        {
            return false;
        }

        if (!jobSearch.TryGetProperty("results", out JsonElement results))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Extracts job results from a response document.
    /// </summary>
    public IEnumerable<JsonElement> ExtractJobs(JsonDocument document)
    {
        if (!document.RootElement.TryGetProperty("data", out JsonElement data))
        {
            return Enumerable.Empty<JsonElement>();
        }

        if (!data.TryGetProperty("jobSearch", out JsonElement jobSearch))
        {
            return Enumerable.Empty<JsonElement>();
        }

        if (!jobSearch.TryGetProperty("results", out JsonElement results))
        {
            return Enumerable.Empty<JsonElement>();
        }

        if (results.ValueKind != JsonValueKind.Array)
        {
            return Enumerable.Empty<JsonElement>();
        }

        return results.EnumerateArray().ToList();
    }
}
