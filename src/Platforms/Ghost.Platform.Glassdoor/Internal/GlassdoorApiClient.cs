using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Ghost.Platform.Glassdoor.Internal;

public sealed class GlassdoorApiClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(2); // Conservative rate limiting
    private bool _disposed;

    public GlassdoorApiClient(HttpClient http)
    {
        _http = http;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _rateLimitSemaphore?.Dispose();
            _disposed = true;
        }
    }

    public async Task<string?> GetCsrfTokenAsync(CancellationToken ct = default)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm?loc=US");
            request.Headers.Host = "www.glassdoor.com";
            foreach (var header in GlassdoorConstants.CsrfHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var res = await _http.SendAsync(request, ct);
            var html = await res.Content.ReadAsStringAsync(ct);

            // DEBUG: Write raw HTML to file
            try { System.IO.File.WriteAllText("logs/glassdoor_csrf.html", html); } catch { }

            // Check for consent/blocking pages
            if (IsConsentOrBlockedPage(html))
            {
                // Try alternative approach with different headers
                var altRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm");
                foreach (var header in GlassdoorConstants.AlternativeHeaders)
                {
                    altRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                var altRes = await _http.SendAsync(altRequest, ct);
                html = await altRes.Content.ReadAsStringAsync(ct);

                try { System.IO.File.WriteAllText("logs/glassdoor_csrf_alt.html", html); } catch { }

                // If still blocked, use fallback token
                if (IsConsentOrBlockedPage(html))
                {
                    return GlassdoorConstants.FallbackToken;
                }
            }

            // Multiple CSRF token extraction patterns with fallbacks
            string? token = ExtractCsrfTokenWithMultiplePatterns(html);
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
        }
        catch { }
        return GlassdoorConstants.FallbackToken;
    }

    /// <summary>
    /// Build GraphQL payload based on JobSpy's structure
    /// </summary>
    private static string BuildSearchPayload(string keyword, string? location)
    {
        // Default location ID for remote (from JobSpy)
        var locationId = 11047;
        var locationType = "STATE";

        // Build filter params (empty for basic search)
        var filterParams = new List<object>();

        // Build payload matching JobSpy's structure
        var payloadObj = new
        {
            operationName = "JobSearchResultsQuery",
            variables = new
            {
                excludeJobListingIds = new List<int>(),
                filterParams = filterParams,
                keyword = keyword,
                numJobsToShow = 30,
                locationType = locationType,
                locationId = locationId,
                parameterUrlInput = $"IL.0,12_I{locationType}{locationId}",
                pageNumber = 1,
                pageCursor = (string?)null,
                fromage = (int?)null,
                sort = "date"
            },
            query = GlassdoorConstants.JobSearchQuery
        };

        return JsonSerializer.Serialize(new[] { payloadObj });
    }

    /// <summary>
    /// Extract CSRF token using multiple patterns with fallbacks
    /// Based on JobSpy's pattern: r'"token":\s*"([^"]+)"'
    /// </summary>
    private static string? ExtractCsrfTokenWithMultiplePatterns(string html)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        // Primary pattern from JobSpy - most reliable
        var primaryPattern = "\"token\"\\s*:\\s*\"([^\"]+)\"";
        var match = Regex.Match(html, primaryPattern);
        if (match.Success && match.Groups.Count > 1)
        {
            var token = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(token) && token.Length > 10)
            {
                return token;
            }
        }

        // Fallback patterns for different HTML structures
        var fallbackPatterns = new[]
        {
            "<meta[^>]*csrf-token[^>]*content=\"([^\"]+)\"[^>]*>",
            "window\\.\\w+\\s*=\\s*\\{\\s*\"token\"\\s*:\\s*\"([^\"]+)\"",
            "\"gd-csrf-token\"\\s*:\\s*\"([^\"]+)\"",
            "data-csrf-token=\"([^\"]+)\"",
            "token\\\"\\s*:\\s*\\\"([^\\\"]+)\\\""
        };

        foreach (var pattern in fallbackPatterns)
        {
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var token = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(token) && token.Length > 10)
                {
                    return token;
                }
            }
        }

        return null;
    }

    public async Task<string?> SearchAsync(string keyword, string? location = null, string? csrfToken = null, CancellationToken ct = default)
    {
        var token = csrfToken ?? await GetCsrfTokenAsync(ct);

        // Build payload based on JobSpy's structure
        var payload = BuildSearchPayload(keyword, location);

        var request = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        foreach (var header in GlassdoorConstants.GraphHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.TryAddWithoutValidation("gd-csrf-token", token);
        }

        // Enhanced retry logic with rate limiting based on JobSpy patterns
        for (int attempt = 0; attempt < 3; attempt++)
        {
            // Apply rate limiting between requests
            await ApplyRateLimitAsync(ct);

            // Create a new request message for each attempt to avoid reuse issues
            var retryRequest = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
            };

            foreach (var header in GlassdoorConstants.GraphHeaders)
            {
                retryRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (!string.IsNullOrEmpty(token))
            {
                retryRequest.Headers.TryAddWithoutValidation("gd-csrf-token", token);
            }

            try
            {
                var res = await _http.SendAsync(retryRequest, ct);
                var json = await res.Content.ReadAsStringAsync(ct);

                // DEBUG: Write raw JSON to file
                try { System.IO.File.WriteAllText($"logs/glassdoor_search_{attempt}.json", json); } catch { }

                // Parse GraphQL response for errors
                var (hasErrors, shouldRetry) = ParseGraphQLErrors(json);
                
                if (hasErrors)
                {
                    if (shouldRetry && attempt < 2) // Not the last attempt
                    {
                        // Wait with exponential backoff plus jitter
                        var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt) + new Random().NextDouble());
                        await Task.Delay(delay, ct);
                        continue;
                    }
                    else
                    {
                        // Non-retryable error or last attempt
                        return null;
                    }
                }

                if (res.IsSuccessStatusCode)
                {
                    return json;
                }
                
                // Handle HTTP status codes
                if (res.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                {
                    if (attempt < 2)
                    {
                        // Rate limited - wait longer
                        await Task.Delay(TimeSpan.FromSeconds(10), ct);
                        continue;
                    }
                }
                else if ((int)res.StatusCode >= 500)
                {
                    // Server error - retry with backoff
                    if (attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                        continue;
                    }
                }
                
                // If we get here and it's not the last attempt, wait and retry
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                }
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
            {
                // Handle explicit rate limit exceptions
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), ct);
                    continue;
                }
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                // Operation was cancelled
                return null;
            }
            catch (Exception)
            {
                // Other network errors - retry with backoff
                if (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
                    continue;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Apply rate limiting between requests to avoid hitting API limits
    /// Based on JobSpy's conservative rate limiting patterns
    /// </summary>
    private async Task ApplyRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitSemaphore.WaitAsync(ct);
        try
        {
            var timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _rateLimitDelay)
            {
                var waitTime = _rateLimitDelay - timeSinceLastRequest;
                await Task.Delay(waitTime, ct);
            }
            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLimitSemaphore.Release();
        }
    }

    /// <summary>
    /// Check if the HTML content indicates a consent or blocked page
    /// Based on Google Jobs consent handling patterns
    /// </summary>
    private static bool IsConsentOrBlockedPage(string html)
    {
        if (string.IsNullOrEmpty(html))
            return true;

        // Check for common consent/blocking indicators
        return html.Contains("consent", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("robot check", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("captcha", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("verify", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("human verification", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("security check", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("terms of service", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("privacy policy", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("cookie policy", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("before you continue", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("accept cookies", StringComparison.OrdinalIgnoreCase) ||
               html.Contains("manage cookies", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parse GraphQL response for errors and determine retry strategy
    /// Based on JobSpy's error handling patterns
    /// </summary>
    private static (bool hasErrors, bool shouldRetry) ParseGraphQLErrors(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return (true, false); // Empty response - don't retry
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for GraphQL errors array
            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    if (error.ValueKind == JsonValueKind.Object)
                    {
                        // Extract error message
                        var message = error.TryGetProperty("message", out var msg) 
                            ? msg.GetString() ?? "" 
                            : "";

                        // Determine retry strategy based on error type
                        if (message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("throttled", StringComparison.OrdinalIgnoreCase) ||
                            message.Contains("too many requests", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, true); // Rate limit - retry
                        }
                        else if (message.Contains("server error", StringComparison.OrdinalIgnoreCase) ||
                                 message.Contains("internal error", StringComparison.OrdinalIgnoreCase) ||
                                 message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, true); // Server error - retry
                        }
                        else if (message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase) ||
                                 message.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
                                 message.Contains("invalid token", StringComparison.OrdinalIgnoreCase))
                        {
                            return (true, false); // Auth error - don't retry
                        }
                    }
                }
                return (true, false); // Generic GraphQL error - don't retry
            }

            // Check for success indicator
            if (root.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
            {
                return (false, false); // Success - no errors
            }
        }
        catch (JsonException)
        {
            // Invalid JSON - don't retry
            return (true, false);
        }

        return (true, false); // Default to error with no retry
    }
}

// lightweight JsonDocument builder placeholder for potential extension (keeps code flexible)
internal sealed class JsonDocumentBuilder : IDisposable
{
    public void Dispose() { }
}
