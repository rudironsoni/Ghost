using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Ghost.Http;
using Ghost.Platform.Storage.Session;
using Microsoft.Extensions.Logging;
using Polly;

namespace Ghost.Plugin.Glassdoor.Internal;

public sealed class GlassdoorApiClient : IDisposable
{
    private readonly HttpClient? _http;
    private readonly ISessionOrchestrator? _sessionOrchestrator;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(2); // Conservative rate limiting
    private readonly ILogger<GlassdoorApiClient>? _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private bool _disposed;
    private string? _currentSessionId;

    private static readonly Action<ILogger, Exception?> LogSearchFailed =
        LoggerMessage.Define(LogLevel.Warning, new EventId(1, nameof(LogSearchFailed)), "Glassdoor search request failed after retries");

    private static readonly Action<ILogger, string, Exception?> LogSessionAllocated =
        LoggerMessage.Define<string>(LogLevel.Information, new EventId(3001, "SessionAllocated"), "Allocated session {SessionId} for Glassdoor requests");

    private static readonly Action<ILogger, string, Exception?> LogSessionRecycled =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(3002, "SessionRecycled"), "Recycling unhealthy session {SessionId}");

    private static readonly Action<ILogger, string, Exception?> LogSessionGetFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3003, "SessionGetFailed"), "Failed to get HTTP session {SessionId}");

    private static readonly Action<ILogger, string, Exception?> LogSessionHealthCheckFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3004, "SessionHealthCheckFailed"), "Failed to check session health for {SessionId}");

    private static readonly Action<ILogger, string, Exception?> LogSessionCloseFailed =
        LoggerMessage.Define<string>(LogLevel.Error, new EventId(3005, "SessionCloseFailed"), "Failed to close session {SessionId} during disposal");

    /// <summary>
    /// Legacy constructor for backward compatibility. Uses direct HttpClient.
    /// </summary>
    public GlassdoorApiClient(HttpClient http, ILogger<GlassdoorApiClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
        _sessionOrchestrator = null;
        _logger = logger;
        _retryPolicy = EnhancedRetryPolicy.CreatePolicy(logger, maxRetries: 4, enableJitter: true);
    }

    /// <summary>
    /// Modern constructor with SessionOrchestrator support for session continuity and health monitoring.
    /// </summary>
    public GlassdoorApiClient(ISessionOrchestrator sessionOrchestrator, ILogger<GlassdoorApiClient>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(sessionOrchestrator);
        _http = null;
        _sessionOrchestrator = sessionOrchestrator;
        _logger = logger;
        _retryPolicy = EnhancedRetryPolicy.CreatePolicy(logger, maxRetries: 4, enableJitter: true);
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

            // Avoid blocking call in Dispose - just release the session reference
            // The SessionOrchestrator will handle cleanup on its own lifecycle
            if (_currentSessionId != null && _sessionOrchestrator != null)
            {
                try
                {
                    // Fire and forget - avoid deadlock from GetAwaiter().GetResult()
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _sessionOrchestrator.CloseSessionAsync(_currentSessionId, default).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            if (_logger != null) LogSessionCloseFailed(_logger, _currentSessionId, ex);
                        }
                    });
                }
                catch (Exception ex)
                {
                    if (_logger != null) LogSessionCloseFailed(_logger, _currentSessionId, ex);
                }
            }

            _disposed = true;
        }
    }

    public async Task<string?> GetCsrfTokenAsync(CancellationToken ct = default)
    {
        if (_sessionOrchestrator != null)
        {
            return await GetCsrfTokenWithOrchestratorAsync(ct).ConfigureAwait(false);
        }
        else
        {
            return await GetCsrfTokenLegacyAsync(ct).ConfigureAwait(false);
        }
    }

    private async Task<string?> GetCsrfTokenWithOrchestratorAsync(CancellationToken ct)
    {
        try
        {
            LogTokenExtraction("Starting CSRF token extraction from Glassdoor with SessionOrchestrator");

            var context = new SessionAllocationContext(
                PlatformName: "Glassdoor",
                CountryCode: "US",
                SessionType: SessionType.Http,
                ComplexityScore: 40,
                Metadata: new Dictionary<string, string>
                {
                    ["Operation"] = "CsrfTokenExtraction"
                }
            );

            string sessionId = await _sessionOrchestrator!.AllocateSessionAsync(context, ct).ConfigureAwait(false);

            try
            {
                RotatingProxySession? httpSession = await _sessionOrchestrator.GetHttpSessionAsync(sessionId, ct).ConfigureAwait(false);
                if (httpSession == null)
                {
                    LogTokenExtraction($"Failed to get HTTP session {sessionId}");
                    return GlassdoorConstants.FallbackToken;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm?loc=US");
                request.Headers.Host = "www.glassdoor.com";
                foreach (KeyValuePair<string, string> header in GlassdoorConstants.CsrfHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                HttpResponseMessage res = await _retryPolicy.ExecuteAsync(async () =>
                    await httpSession.ExecuteAsync(() => request, ct).ConfigureAwait(false)).ConfigureAwait(false);
                string html = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                try { await System.IO.File.WriteAllTextAsync("logs/glassdoor_csrf.html", html, ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write debug file: {ex.Message}"); }

                LogTokenExtraction($"Received HTML response: {html.Length} characters");

                if (IsConsentOrBlockedPage(html))
                {
                    LogTokenExtraction("Detected consent or blocked page, trying alternative approach");

                    var altRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm");
                    foreach (KeyValuePair<string, string> header in GlassdoorConstants.AlternativeHeaders)
                    {
                        altRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    HttpResponseMessage altRes = await _retryPolicy.ExecuteAsync(async () =>
                        await httpSession.ExecuteAsync(() => altRequest, ct).ConfigureAwait(false)).ConfigureAwait(false);
                    html = await altRes.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                    try { await System.IO.File.WriteAllTextAsync("logs/glassdoor_csrf_alt.html", html, ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write debug file: {ex.Message}"); }

                    if (IsConsentOrBlockedPage(html))
                    {
                        LogTokenExtraction("Still blocked after alternative approach, using fallback token");
                        return GlassdoorConstants.FallbackToken;
                    }
                }

                string? token = ExtractCsrfTokenWithMultiplePatterns(html);
                if (!string.IsNullOrEmpty(token))
                {
                    LogTokenExtraction($"Successfully extracted token: {token.Substring(0, Math.Min(10, token.Length))}... (length: {token.Length})");

                    bool isValid = await ValidateTokenWithOrchestratorAsync(token, httpSession, ct).ConfigureAwait(false);
                    if (isValid)
                    {
                        LogTokenExtraction("Token validation successful");
                        return token;
                    }
                    else
                    {
                        LogTokenExtraction("Token validation failed, using fallback token");
                    }
                }
                else
                {
                    LogTokenExtraction("Failed to extract token from HTML");
                }
            }
            finally
            {
                await _sessionOrchestrator.CloseSessionAsync(sessionId, ct).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            LogTokenExtraction($"Exception during token extraction: {ex.Message}");
        }
        LogTokenExtraction("Using fallback token");
        return GlassdoorConstants.FallbackToken;
    }

    private async Task<string?> GetCsrfTokenLegacyAsync(CancellationToken ct)
    {
        try
        {
            LogTokenExtraction("Starting CSRF token extraction from Glassdoor");

            var request = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm?loc=US");
            request.Headers.Host = "www.glassdoor.com";
            foreach (KeyValuePair<string, string> header in GlassdoorConstants.CsrfHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            HttpResponseMessage res = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(request, ct).ConfigureAwait(false)).ConfigureAwait(false);
            string html = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // DEBUG: Write raw HTML to file
            try { await System.IO.File.WriteAllTextAsync("logs/glassdoor_csrf.html", html, ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write debug file: {ex.Message}"); }

            LogTokenExtraction($"Received HTML response: {html.Length} characters");

            // Check for consent/blocking pages
            if (IsConsentOrBlockedPage(html))
            {
                LogTokenExtraction("Detected consent or blocked page, trying alternative approach");

                // Try alternative approach with different headers
                var altRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm");
                foreach (KeyValuePair<string, string> header in GlassdoorConstants.AlternativeHeaders)
                {
                    altRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                HttpResponseMessage altRes = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(altRequest, ct).ConfigureAwait(false)).ConfigureAwait(false);
                html = await altRes.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                try { await System.IO.File.WriteAllTextAsync("logs/glassdoor_csrf_alt.html", html, ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write debug file: {ex.Message}"); }

                // If still blocked, use fallback token
                if (IsConsentOrBlockedPage(html))
                {
                    LogTokenExtraction("Still blocked after alternative approach, using fallback token");
                    return GlassdoorConstants.FallbackToken;
                }
            }

            // Multiple CSRF token extraction patterns with fallbacks
            string? token = ExtractCsrfTokenWithMultiplePatterns(html);
            if (!string.IsNullOrEmpty(token))
            {
                LogTokenExtraction($"Successfully extracted token: {token.Substring(0, Math.Min(10, token.Length))}... (length: {token.Length})");

                // Validate the token by testing it against the API
                bool isValid = await ValidateTokenAsync(token, ct).ConfigureAwait(false);
                if (isValid)
                {
                    LogTokenExtraction("Token validation successful");
                    return token;
                }
                else
                {
                    LogTokenExtraction("Token validation failed, using fallback token");
                }
            }
            else
            {
                LogTokenExtraction("Failed to extract token from HTML");
            }
        }
        catch (Exception ex)
        {
            LogTokenExtraction($"Exception during token extraction: {ex.Message}");
        }
        LogTokenExtraction("Using fallback token");
        return GlassdoorConstants.FallbackToken;
    }

    /// <summary>
    /// Log token extraction events for debugging
    /// </summary>
    private static void LogTokenExtraction(string message)
    {
        try
        {
            string logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}\n";
            System.IO.File.AppendAllText("logs/glassdoor_token_extraction.log", logMessage);
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write token log: {ex.Message}"); }
    }

    /// <summary>
    /// Validate extracted token by testing it against the API
    /// </summary>
    private async Task<bool> ValidateTokenWithOrchestratorAsync(string token, RotatingProxySession httpSession, CancellationToken ct)
    {
        try
        {
            LogTokenExtraction($"Validating token: {token.Substring(0, Math.Min(10, token.Length))}...");

            string testPayload = JsonSerializer.Serialize(new[]
            {
                new
                {
                    operationName = "JobSearchResultsQuery",
                    variables = new
                    {
                        excludeJobListingIds = Array.Empty<int>(),
                        filterParams = Array.Empty<object>(),
                        keyword = "test",
                        numJobsToShow = 1,
                        locationType = "STATE",
                        locationId = 11047,
                        parameterUrlInput = "IL.0,12_ISTATE11047",
                        pageNumber = 1,
                        pageCursor = (string?)null,
                        fromage = (int?)null,
                        sort = "date"
                    },
                    query = GlassdoorConstants.JobSearchQuery
                }
            });

            var request = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
            {
                Content = new StringContent(testPayload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
            };

            foreach (KeyValuePair<string, string> header in GlassdoorConstants.GraphHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Headers.TryAddWithoutValidation("gd-csrf-token", token);

            HttpResponseMessage res = await _retryPolicy.ExecuteAsync(async () =>
                await httpSession.ExecuteAsync(() => request, ct).ConfigureAwait(false)).ConfigureAwait(false);
            string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            (bool hasErrors, bool shouldRetry) = ParseGraphQLErrors(json);

            if (hasErrors)
            {
                LogTokenExtraction($"Token validation failed: API returned errors");
                return false;
            }

            if (res.IsSuccessStatusCode)
            {
                LogTokenExtraction($"Token validation successful: API returned {res.StatusCode}");
                return true;
            }

            LogTokenExtraction($"Token validation failed: API returned {res.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            LogTokenExtraction($"Token validation exception: {ex.Message}");
            return false;
        }
    }

    private async Task<bool> ValidateTokenAsync(string token, CancellationToken ct)
    {
        try
        {
            LogTokenExtraction($"Validating token: {token.Substring(0, Math.Min(10, token.Length))}...");

            // Create a minimal test payload
            string testPayload = JsonSerializer.Serialize(new[]
            {
                new
                {
                    operationName = "JobSearchResultsQuery",
                    variables = new
                    {
                        excludeJobListingIds = Array.Empty<int>(),
                        filterParams = Array.Empty<object>(),
                        keyword = "test",
                        numJobsToShow = 1,
                        locationType = "STATE",
                        locationId = 11047,
                        parameterUrlInput = "IL.0,12_ISTATE11047",
                        pageNumber = 1,
                        pageCursor = (string?)null,
                        fromage = (int?)null,
                        sort = "date"
                    },
                    query = GlassdoorConstants.JobSearchQuery
                }
            });

            var request = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
            {
                Content = new StringContent(testPayload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
            };

            foreach (KeyValuePair<string, string> header in GlassdoorConstants.GraphHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Headers.TryAddWithoutValidation("gd-csrf-token", token);

            HttpResponseMessage res = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(request, ct).ConfigureAwait(false)).ConfigureAwait(false);
            string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Check if response is valid (not an auth error)
            (bool hasErrors, bool shouldRetry) = ParseGraphQLErrors(json);

            if (hasErrors)
            {
                LogTokenExtraction($"Token validation failed: API returned errors");
                return false;
            }

            if (res.IsSuccessStatusCode)
            {
                LogTokenExtraction($"Token validation successful: API returned {res.StatusCode}");
                return true;
            }

            LogTokenExtraction($"Token validation failed: API returned {res.StatusCode}");
            return false;
        }
        catch (Exception ex)
        {
            LogTokenExtraction($"Token validation exception: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Enhanced CSRF token extraction with multiple patterns including JSON-based extraction
    /// </summary>
    private static string? ExtractCsrfTokenWithMultiplePatterns(string html)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        // Primary pattern from JobSpy - most reliable
        string primaryPattern = "\"token\"\\s*:\\s*\"([^\"]+)\"";
        Match match = Regex.Match(html, primaryPattern);
        if (match.Success && match.Groups.Count > 1)
        {
            string token = match.Groups[1].Value;
            if (!string.IsNullOrEmpty(token) && token.Length > 10)
            {
                return token;
            }
        }

        // Enhanced fallback patterns for different HTML structures
        string[] fallbackPatterns = new[]
        {
            "<meta[^>]*csrf-token[^>]*content=\"([^\"]+)\"[^>]*>",
            "window\\.\\w+\\s*=\\s*\\{\\s*\"token\"\\s*:\\s*\"([^\"]+)\"",
            "\"gd-csrf-token\"\\s*:\\s*\"([^\"]+)\"",
            "data-csrf-token=\"([^\"]+)\"",
            "token\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
            // New patterns for modern Glassdoor structure
            "window\\.__INITIAL_STATE__\\s*=\\s*(\\{.*?\\});",
            "window\\.__DATA__\\s*=\\s*(\\{.*?\\});",
            "<script[^>]*id=\"__INITIAL_STATE__\"[^>]*type=\"application/json\"[^>]*>(.*?)</script>",
            // Look for any script tag containing csrf or token
            "<script[^>]*>(.*?csrf.*?)</script>",
            "<script[^>]*>(.*?token.*?)</script>"
        };

        foreach (string? pattern in fallbackPatterns)
        {
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Groups.Count > 1)
            {
                string token = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(token) && token.Length > 10)
                {
                    // Try to extract token from JSON content if it's a JSON object
                    string? extractedToken = ExtractTokenFromJsonContent(token);
                    if (!string.IsNullOrEmpty(extractedToken))
                    {
                        return extractedToken;
                    }
                    return token;
                }
            }
        }

        // JSON-based extraction: Parse all JSON script tags and search recursively
        string jsonScriptPattern = @"<script[^>]*type\s*=\s*[""']application/json[""'][^>]*>(.*?)</script>";
        MatchCollection jsonMatches = Regex.Matches(html, jsonScriptPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        foreach (Match jsonMatch in jsonMatches)
        {
            string jsonContent = jsonMatch.Groups[1].Value;
            string? token = ExtractTokenFromJsonRecursively(jsonContent);
            if (!string.IsNullOrEmpty(token))
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>
    /// Extract token from JSON content by parsing it as JSON
    /// </summary>
    private static string? ExtractTokenFromJsonContent(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            return FindTokenInJsonElement(doc.RootElement);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Recursively search for token in JSON structure
    /// </summary>
    private static string? FindTokenInJsonElement(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            string? value = element.GetString();
            if (!string.IsNullOrEmpty(value) && value.Length > 10 && (value.Contains("csrf") || value.Contains("token")))
            {
                return value;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                string propertyName = property.Name.ToLowerInvariant();
                if (propertyName.Contains("token") || propertyName.Contains("csrf"))
                {
                    string? token = FindTokenInJsonElement(property.Value);
                    if (!string.IsNullOrEmpty(token))
                    {
                        return token;
                    }
                }
                else
                {
                    string? token = FindTokenInJsonElement(property.Value);
                    if (!string.IsNullOrEmpty(token))
                    {
                        return token;
                    }
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                string? token = FindTokenInJsonElement(item);
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Recursively search for token in JSON string content
    /// </summary>
    private static string? ExtractTokenFromJsonRecursively(string jsonContent)
    {
        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            return FindTokenInJsonElement(doc.RootElement);
        }
        catch
        {
            // If JSON parsing fails, try regex patterns on the raw content
            string[] tokenPatterns = new[]
            {
                "\"token\"\\s*:\\s*\"([^\"]+)\"",
                "\"csrf\"\\s*:\\s*\"([^\"]+)\"",
                "\"gd-csrf-token\"\\s*:\\s*\"([^\"]+)\""
            };

            foreach (string? pattern in tokenPatterns)
            {
                Match match = Regex.Match(jsonContent, pattern, RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    string token = match.Groups[1].Value;
                    if (!string.IsNullOrEmpty(token) && token.Length > 10)
                    {
                        return token;
                    }
                }
            }
            return null;
        }
    }

    /// <summary>
    /// Build GraphQL payload based on JobSpy's structure
    /// </summary>
    private static string BuildSearchPayload(string keyword, string? location)
    {
        // NOTE: Previously this method ignored the `location` parameter and always
        // used a hardcoded locationId = 11047 (remote/US). That caused searches for
        // other locations (e.g. "Spain") to return the same results as remote.
        //
        // Simple fix: respect the incoming `location` string by mapping common
        // location names to approximate Glassdoor locationId/locationType values.
        // This is intentionally small and non-invasive: we do not change method
        // signatures or add external dependencies. Unknown locations fall back
        // to the original default (remote) and we log the resolved mapping for
        // visibility.

        // Default location ID for remote (from JobSpy)
        int defaultLocationId = 11047;
        string defaultLocationType = "STATE";

        int resolvedLocationId = defaultLocationId;
        string resolvedLocationType = defaultLocationType;

        if (!string.IsNullOrWhiteSpace(location))
        {
            // Normalize
            string normalizedLocation = location.Trim().ToLowerInvariant();

            // Basic mapping for common locations. These IDs are best-effort and
            // may need refinement; they are chosen to change the search scope
            // (country vs state) so results differ from the default remote ID.
            switch (normalizedLocation)
            {
                case "remote":
                case "anywhere":
                    resolvedLocationId = 11047; // remote/state default
                    resolvedLocationType = "STATE";
                    break;
                case "spain":
                case "es":
                case "españa":
                    // Use a country-level location type for Spain. The numeric ID
                    // here is an approximation; if you have an authoritative ID,
                    // replace it. Using COUNTRY will change how results are filtered.
                    resolvedLocationId = 1999;
                    resolvedLocationType = "COUNTRY";
                    break;
                case "united states":
                case "united states of america":
                case "us":
                case "usa":
                    resolvedLocationId = 1;
                    resolvedLocationType = "COUNTRY";
                    break;
                case "united kingdom":
                case "uk":
                case "gb":
                case "great britain":
                    resolvedLocationId = 224; // approximate country id for UK
                    resolvedLocationType = "COUNTRY";
                    break;
                case var locationValue when locationValue.StartsWith("province:", StringComparison.Ordinal) || locationValue.StartsWith("state:", StringComparison.Ordinal):
                    // Allow callers to pass "state:11047" or "province:5" to force an id
                    string[] parts = locationValue.Split(':', 2);
                    if (parts.Length == 2 && int.TryParse(parts[1], out int parsedId))
                    {
                        resolvedLocationId = parsedId;
                        resolvedLocationType = locationValue.StartsWith("province:", StringComparison.Ordinal) ? "PROVINCE" : "STATE";
                    }
                    break;
                default:
                    // Unknown free-text location: leave fallback but log the value
                    try { System.IO.File.AppendAllText("logs/glassdoor_location_resolve.log", $"Unknown location '{location}' - falling back to default ({defaultLocationId})\n"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex.Message}"); }
                    break;
            }
        }
        else
        {
            // No location provided - log that we're using default remote
            try { System.IO.File.AppendAllText("logs/glassdoor_location_resolve.log", $"No location provided - using default ({defaultLocationId})\n"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex.Message}"); }
        }

        // Also log the final resolved mapping for transparency
        try { System.IO.File.AppendAllText("logs/glassdoor_location_resolve.log", $"Resolved location '{location}' => id={resolvedLocationId}, type={resolvedLocationType}\n"); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write log: {ex.Message}"); }

        // Build filter params (empty for basic search)
        List<object> filterParams = [];

        // Build payload matching JobSpy's structure
        var payloadObj = new
        {
            operationName = "JobSearchResultsQuery",
            variables = new
            {
                excludeJobListingIds = Array.Empty<int>(),
                filterParams = filterParams,
                keyword = keyword,
                numJobsToShow = 30,
                locationType = resolvedLocationType,
                locationId = resolvedLocationId,
                parameterUrlInput = $"IL.0,12_I{resolvedLocationType}{resolvedLocationId}",
                pageNumber = 1,
                pageCursor = (string?)null,
                fromage = (int?)null,
                sort = "date"
            },
            query = GlassdoorConstants.JobSearchQuery
        };

        return JsonSerializer.Serialize(new[] { payloadObj });
    }

    public async Task<string?> SearchAsync(string keyword, string? location = null, string? csrfToken = null, CancellationToken ct = default)
    {
        LogTokenExtraction($"SearchAsync: Starting search for keyword='{keyword}', location='{location}', csrfToken={csrfToken != null}");

        // First try the simple HTTP scraper fallback (no proxy, no browser, no CSRF needed)
        try
        {
            LogTokenExtraction($"SearchAsync: Attempting simple HTTP scraper first");
            string? result = await SearchWithSimpleHttpAsync(keyword, location, ct).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(result))
            {
                LogTokenExtraction($"Simple HTTP scraper succeeded for query: {keyword}");
                return result;
            }
            LogTokenExtraction($"Simple HTTP scraper returned empty result");
        }
        catch (Exception ex)
        {
            LogTokenExtraction($"Simple HTTP scraper failed: {ex.GetType().Name} - {ex.Message}, falling back to GraphQL");
        }

        // Fallback to original GraphQL approach if simple scraper fails
        if (_sessionOrchestrator != null)
        {
            LogTokenExtraction($"SearchAsync: Using SessionOrchestrator approach");
            return await SearchWithOrchestratorAsync(keyword, location, csrfToken, ct).ConfigureAwait(false);
        }
        else
        {
            LogTokenExtraction($"SearchAsync: Using Legacy approach");
            return await SearchLegacyAsync(keyword, location, csrfToken, ct).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Simple HTTP-based scraper that directly fetches Glassdoor job search HTML
    /// without requiring SOCKS5 proxies, browser automation, or CSRF tokens.
    /// </summary>
#pragma warning disable CA1822 // Mark members as static
    private async Task<string?> SearchWithSimpleHttpAsync(string keyword, string? location, CancellationToken ct)
#pragma warning restore CA1822 // Mark members as static
    {
        try
        {
            // Build the search URL
            string encodedKeyword = Uri.EscapeDataString(keyword);
            string encodedLocation = Uri.EscapeDataString(location ?? "");
            string searchUrl = $"https://www.glassdoor.com/Job/jobs.htm?sc.keyword={encodedKeyword}";

            if (!string.IsNullOrWhiteSpace(location))
            {
                searchUrl += $"&locT=C&locId=&locKeyword={encodedLocation}";
            }

            LogTokenExtraction($"Simple HTTP: Fetching {searchUrl}");

            // Create a simple HTTP client with realistic headers
            using var handler = new HttpClientHandler
            {
                AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5
            };

            using var httpClient = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromSeconds(30)
            };

            var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);

            // Add realistic browser headers
            request.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            request.Headers.TryAddWithoutValidation("Accept-Encoding", "gzip, deflate, br");
            request.Headers.TryAddWithoutValidation("Connection", "keep-alive");
            request.Headers.TryAddWithoutValidation("Upgrade-Insecure-Requests", "1");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Dest", "document");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Mode", "navigate");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-Site", "none");
            request.Headers.TryAddWithoutValidation("Sec-Fetch-User", "?1");

            HttpResponseMessage response = await httpClient.SendAsync(request, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            string html = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // Log the HTML for debugging
            try { await System.IO.File.WriteAllTextAsync("logs/glassdoor_simple_http.html", html, ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write debug file: {ex.Message}"); }

            LogTokenExtraction($"Simple HTTP: Received {html.Length} characters");

            // Parse the HTML to extract job data
            string? jobsJson = ParseHtmlToJobsJson(html);

            if (!string.IsNullOrEmpty(jobsJson))
            {
                try { await System.IO.File.WriteAllTextAsync("logs/glassdoor_simple_http_parsed.json", jobsJson, ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write debug file: {ex.Message}"); }
                LogTokenExtraction($"Simple HTTP: Successfully parsed {jobsJson.Length} characters of JSON");
                return jobsJson;
            }

            LogTokenExtraction("Simple HTTP: Failed to extract job data from HTML");
            return null;
        }
        catch (Exception ex)
        {
            LogTokenExtraction($"Simple HTTP scraper exception: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Parse Glassdoor HTML job search results into JSON format compatible with existing parser
    /// </summary>
    private static string? ParseHtmlToJobsJson(string html)
    {
        if (string.IsNullOrEmpty(html))
            return null;

        try
        {
            List<object> jobs = [];

            // Look for embedded JSON data in script tags (modern Glassdoor often embeds data)
            // Pattern 1: Look for window.gdInitialState or similar
            string[] jsonPatterns = new[]
            {
                @"window\.gdInitialState\s*=\s*({.*?});",
                @"window\.__INITIAL_STATE__\s*=\s*({.*?});",
                @"window\.__NEXT_DATA__\s*=\s*({.*?});",
                @"<script[^>]*type\s*=\s*[""']application/json[""'][^>]*id\s*=\s*[""']__NEXT_DATA__[""'][^>]*>(.*?)</script>",
                @"<script[^>]*id\s*=\s*[""']__NEXT_DATA__[""'][^>]*type\s*=\s*[""']application/json[""'][^>]*>(.*?)</script>"
            };

            foreach (string? pattern in jsonPatterns)
            {
                Match match = Regex.Match(html, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);
                if (match.Success && match.Groups.Count > 1)
                {
                    string jsonContent = match.Groups[1].Value;
                    try
                    {
                        using var doc = JsonDocument.Parse(jsonContent);
                        List<object> extractedJobs = ExtractJobsFromJsonElement(doc.RootElement);
                        if (extractedJobs.Count > 0)
                        {
                            jobs.AddRange(extractedJobs);
                            break;
                        }
                    }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Job extraction failed: {ex.Message}"); }
                }
            }

            // Fallback: Parse HTML structure for job cards
            if (jobs.Count == 0)
            {
                jobs.AddRange(ParseJobCardsFromHtml(html));
            }

            if (jobs.Count == 0)
                return null;

            // Return JSON in a format compatible with existing parser
            string result = JsonSerializer.Serialize(new
            {
                data = new
                {
                    jobListings = new
                    {
                        jobListings = jobs,
                        totalJobsCount = jobs.Count
                    }
                }
            });

            return result;
        }
        catch (Exception ex)
        {
            LogTokenExtraction($"ParseHtmlToJobsJson exception: {ex.Message}");
            return null;
        }
    }

    private static List<object> ExtractJobsFromJsonElement(JsonElement element)
    {
        List<object> jobs = [];

        try
        {
            // Recursively search for job data in the JSON structure
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (JsonProperty prop in element.EnumerateObject())
                {
                    // Look for properties that likely contain job listings
                    if (prop.Name.Contains("job", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Contains("listing", StringComparison.OrdinalIgnoreCase) ||
                        prop.Name.Contains("result", StringComparison.OrdinalIgnoreCase))
                    {
                        if (prop.Value.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement item in prop.Value.EnumerateArray())
                            {
                                object? job = ExtractJobFromElement(item);
                                if (job != null)
                                    jobs.Add(job);
                            }
                        }
                        else
                        {
                            jobs.AddRange(ExtractJobsFromJsonElement(prop.Value));
                        }
                    }
                    else
                    {
                        jobs.AddRange(ExtractJobsFromJsonElement(prop.Value));
                    }
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement item in element.EnumerateArray())
                {
                    object? job = ExtractJobFromElement(item);
                    if (job != null)
                        jobs.Add(job);
                    else
                        jobs.AddRange(ExtractJobsFromJsonElement(item));
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Job extraction failed: {ex.Message}"); }

        return jobs;
    }

    private static object? ExtractJobFromElement(JsonElement element)
    {
        try
        {
            // Try to extract job fields
            string? title = null;
            string? company = null;
            string? location = null;
            string? id = null;
            string? description = null;
            string? link = null;

            if (element.ValueKind == JsonValueKind.Object)
            {
                // Try different field name variations
                title = GetJsonString(element, "jobTitleText", "jobTitle", "title", "job_title");
                company = GetJsonString(element, "employerName", "employer", "company", "companyName");
                location = GetJsonString(element, "locationName", "location", "jobLocationCity", "city");
                id = GetJsonString(element, "listingId", "jobId", "id");
                description = GetJsonString(element, "description", "jobDescription");
                link = GetJsonString(element, "jobLink", "link", "url");

                // Check for nested structures (header, job, employer, etc.)
                if (element.TryGetProperty("jobview", out JsonElement jobview))
                {
                    if (jobview.TryGetProperty("header", out JsonElement header))
                    {
                        title ??= GetJsonString(header, "jobTitleText", "jobTitle");
                        location ??= GetJsonString(header, "locationName", "location");
                        link ??= GetJsonString(header, "jobLink");

                        if (header.TryGetProperty("employer", out JsonElement employer))
                        {
                            company ??= GetJsonString(employer, "name");
                        }
                    }

                    if (jobview.TryGetProperty("job", out JsonElement job))
                    {
                        id ??= GetJsonString(job, "listingId");
                        description ??= GetJsonString(job, "description");
                    }
                }

                // Must have at least title to be a valid job
                if (!string.IsNullOrEmpty(title))
                {
                    string deterministicId = id ?? GlassdoorIdGenerator.GenerateDeterministicId(title, company, location, link);
                    return new
                    {
                        jobview = new
                        {
                            header = new
                            {
                                jobTitleText = title,
                                locationName = location,
                                employer = new { name = company },
                                jobLink = link
                            },
                            job = new
                            {
                                listingId = deterministicId,
                                description = description
                            }
                        }
                    };
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Job extraction failed: {ex.Message}"); }

        return null;
    }

    private static string? GetJsonString(JsonElement element, params string[] keys)
    {
        foreach (string key in keys)
        {
            if (element.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? str = value.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                    return str;
            }
        }
        return null;
    }

    private static List<object> ParseJobCardsFromHtml(string html)
    {
        List<object> jobs = [];

        try
        {
            // Use regex to find job card data attributes or structured data
            // Pattern: Look for data-* attributes with job information
            string jobCardPattern = @"<(?:li|div|article)[^>]*(?:data-job-id|data-id|id)[^>]*=[\""']([^\""']+)[\""'][^>]*>.*?(?:data-job-title|class[\""'][^>]*job-title)[^>]*>([^<]+)<.*?(?:data-employer-name|class[\""'][^>]*employer)[^>]*>([^<]+)<.*?(?:data-location|class[\""'][^>]*location)[^>]*>([^<]+)<";

            MatchCollection matches = Regex.Matches(html, jobCardPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            foreach (Match match in matches)
            {
                if (match.Groups.Count >= 5)
                {
                    var job = new
                    {
                        jobview = new
                        {
                            header = new
                            {
                                jobTitleText = match.Groups[2].Value.Trim(),
                                locationName = match.Groups[4].Value.Trim(),
                                employer = new { name = match.Groups[3].Value.Trim() },
                                jobLink = (string?)null
                            },
                            job = new
                            {
                                listingId = match.Groups[1].Value.Trim(),
                                description = (string?)null
                            }
                        }
                    };
                    jobs.Add(job);
                }
            }

            // Alternative pattern: Look for structured data (JSON-LD)
            if (jobs.Count == 0)
            {
                string jsonLdPattern = @"<script[^>]*type\s*=\s*[""']application/ld\+json[""'][^>]*>(.*?)</script>";
                MatchCollection jsonLdMatches = Regex.Matches(html, jsonLdPattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

                foreach (Match match in jsonLdMatches)
                {
                    if (match.Groups.Count > 1)
                    {
                        try
                        {
                            string jsonContent = match.Groups[1].Value;
                            using var doc = JsonDocument.Parse(jsonContent);
                            JsonElement root = doc.RootElement;

                            // Check if it's a JobPosting
                            if (root.TryGetProperty("@type", out JsonElement type) &&
                                type.GetString()?.Contains("JobPosting", StringComparison.OrdinalIgnoreCase) == true)
                            {
                                string? title = GetJsonString(root, "title", "name");
                                string? company = GetJsonString(root, "hiringOrganization", "companyName");
                                string? location = GetJsonString(root, "jobLocation", "addressLocality");
                                string? description = GetJsonString(root, "description");
                                string? id = GetJsonString(root, "identifier", "jobId");

                                if (!string.IsNullOrEmpty(title))
                                {
                                    string deterministicId = id ?? GlassdoorIdGenerator.GenerateDeterministicId(title, company, location, null);
                                    jobs.Add(new
                                    {
                                        jobview = new
                                        {
                                            header = new
                                            {
                                                jobTitleText = title,
                                                locationName = location,
                                                employer = new { name = company },
                                                jobLink = (string?)null
                                            },
                                            job = new
                                            {
                                                listingId = deterministicId,
                                                description = description
                                            }
                                        }
                                    });
                                }
                            }
                        }
                        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Job field extraction failed: {ex.Message}"); }
                    }
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Job list extraction failed: {ex.Message}"); }

        return jobs;
    }

    private async Task<string?> SearchWithOrchestratorAsync(string keyword, string? location, string? csrfToken, CancellationToken ct)
    {
        string affinityKey = $"glassdoor_{keyword}_{location}_{Guid.NewGuid():N}";

        try
        {
            var context = new SessionAllocationContext(
                PlatformName: "Glassdoor",
                CountryCode: "US",
                SessionType: SessionType.Http,
                ComplexityScore: 50,
                Metadata: new Dictionary<string, string>
                {
                    ["Query"] = keyword,
                    ["Location"] = location ?? "Remote"
                }
            );

            var affinityOptions = new SessionAffinityOptions(
                AffinityKey: affinityKey,
                AffinityDuration: TimeSpan.FromMinutes(5),
                AllowFallback: true
            );

            _currentSessionId = await _sessionOrchestrator!.AllocateSessionWithAffinityAsync(context, affinityOptions, ct).ConfigureAwait(false);
            if (_logger != null) LogSessionAllocated(_logger, _currentSessionId, null);

            RotatingProxySession? httpSession = await _sessionOrchestrator.GetHttpSessionAsync(_currentSessionId, ct).ConfigureAwait(false);
            if (httpSession == null)
            {
                if (_logger != null) LogSessionGetFailed(_logger, _currentSessionId, null);
                return null;
            }

            string? token = csrfToken ?? await GetCsrfTokenWithOrchestratorAsync(ct).ConfigureAwait(false);

            string payload = BuildSearchPayload(keyword, location);

            await ApplyRateLimitAsync(ct).ConfigureAwait(false);

            var request = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
            {
                Content = new StringContent(payload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
            };

            foreach (KeyValuePair<string, string> header in GlassdoorConstants.GraphHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.TryAddWithoutValidation("gd-csrf-token", token);
            }

            try
            {
                HttpResponseMessage res = await _retryPolicy.ExecuteAsync(async () =>
                {
                    var retryRequest = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
                    {
                        Content = new StringContent(payload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
                    };

                    foreach (KeyValuePair<string, string> header in GlassdoorConstants.GraphHeaders)
                    {
                        retryRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    if (!string.IsNullOrEmpty(token))
                    {
                        retryRequest.Headers.TryAddWithoutValidation("gd-csrf-token", token);
                    }

                    return await httpSession.ExecuteAsync(() => retryRequest, ct).ConfigureAwait(false);
                }).ConfigureAwait(false);

                string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                try { await System.IO.File.WriteAllTextAsync($"logs/glassdoor_search.json", json, ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write debug file: {ex.Message}"); }

                (bool hasErrors, bool shouldRetry) = ParseGraphQLErrors(json);

                if (hasErrors)
                {
                    await CheckAndRecycleSessionAsync().ConfigureAwait(false);
                    return null;
                }

                if (res.IsSuccessStatusCode)
                {
                    return json;
                }

                return null;
            }
            catch (TaskCanceledException) when (ct.IsCancellationRequested)
            {
                return null;
            }
            catch (Exception ex)
            {
                if (_logger != null)
                {
                    LogSearchFailed(_logger, ex);
                }
                await CheckAndRecycleSessionAsync().ConfigureAwait(false);
                return null;
            }
        }
        finally
        {
            if (_currentSessionId != null)
            {
                await _sessionOrchestrator!.CloseSessionAsync(_currentSessionId, ct).ConfigureAwait(false);
                _currentSessionId = null;
            }
        }
    }

    private async Task<string?> SearchLegacyAsync(string keyword, string? location, string? csrfToken, CancellationToken ct)
    {
        LogTokenExtraction($"SearchLegacyAsync: Starting with keyword='{keyword}', location='{location}'");
        string? token = csrfToken ?? await GetCsrfTokenAsync(ct).ConfigureAwait(false);
        LogTokenExtraction($"SearchLegacyAsync: Got token={token?.Substring(0, Math.Min(10, token?.Length ?? 0)) ?? "null"}");

        // Build payload based on JobSpy's structure
        string payload = BuildSearchPayload(keyword, location);
        LogTokenExtraction($"SearchLegacyAsync: Built payload length={payload.Length}");

        // Apply rate limiting before the request
        await ApplyRateLimitAsync(ct).ConfigureAwait(false);

        var request = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
        };

        foreach (KeyValuePair<string, string> header in GlassdoorConstants.GraphHeaders)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.TryAddWithoutValidation("gd-csrf-token", token);
        }

        try
        {
            LogTokenExtraction($"SearchLegacyAsync: Sending POST request to {GlassdoorConstants.ApiUrl}");
            // Use EnhancedRetryPolicy for automatic retry with exponential backoff
            HttpResponseMessage res = await _retryPolicy.ExecuteAsync(async () =>
            {
                // Create a new request for each retry attempt
                var retryRequest = new HttpRequestMessage(HttpMethod.Post, GlassdoorConstants.ApiUrl)
                {
                    Content = new StringContent(payload, Encoding.UTF8, new MediaTypeHeaderValue("application/json"))
                };

                foreach (KeyValuePair<string, string> header in GlassdoorConstants.GraphHeaders)
                {
                    retryRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                if (!string.IsNullOrEmpty(token))
                {
                    retryRequest.Headers.TryAddWithoutValidation("gd-csrf-token", token);
                }

                return await _http!.SendAsync(retryRequest, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            LogTokenExtraction($"SearchLegacyAsync: Got response StatusCode={res.StatusCode}");
            string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            LogTokenExtraction($"SearchLegacyAsync: Response length={json.Length}");

            // DEBUG: Write raw JSON to file
            try { await System.IO.File.WriteAllTextAsync($"logs/glassdoor_search.json", json, ct).ConfigureAwait(false); } catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to write debug file: {ex.Message}"); }

            // Parse GraphQL response for errors
            (bool hasErrors, bool shouldRetry) = ParseGraphQLErrors(json);
            LogTokenExtraction($"SearchLegacyAsync: ParseGraphQLErrors returned hasErrors={hasErrors}, shouldRetry={shouldRetry}");

            if (hasErrors)
            {
                // EnhancedRetryPolicy handles retries for transient errors
                // If we still have errors after retries, return null
                LogTokenExtraction($"SearchLegacyAsync: Returning null due to GraphQL errors");
                return null;
            }

            if (res.IsSuccessStatusCode)
            {
                LogTokenExtraction($"SearchLegacyAsync: Success, returning JSON");
                return json;
            }

            LogTokenExtraction($"SearchLegacyAsync: Non-success status code, returning null");
            return null;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            LogTokenExtraction($"SearchLegacyAsync: Request cancelled");
            // Operation was cancelled
            return null;
        }
        catch (Exception ex)
        {
            // Log the exception if logger is available
            LogTokenExtraction($"SearchLegacyAsync: Exception {ex.GetType().Name}: {ex.Message}");
            if (_logger != null)
            {
                LogSearchFailed(_logger, ex);
            }
            return null;
        }
    }

    /// <summary>
    /// Apply rate limiting between requests to avoid hitting API limits
    /// Based on JobSpy's conservative rate limiting patterns
    /// </summary>
    private async Task ApplyRateLimitAsync(CancellationToken ct)
    {
        await _rateLimitSemaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            TimeSpan timeSinceLastRequest = DateTime.UtcNow - _lastRequestTime;
            if (timeSinceLastRequest < _rateLimitDelay)
            {
                TimeSpan waitTime = _rateLimitDelay - timeSinceLastRequest;
                await Task.Delay(waitTime, ct).ConfigureAwait(false);
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
            JsonElement root = doc.RootElement;

            // Handle case where root is an array (not a GraphQL response)
            if (root.ValueKind == JsonValueKind.Array)
            {
                return (false, false); // Assume success for array responses
            }

            // Check for GraphQL errors array
            if (root.TryGetProperty("errors", out JsonElement errors) && errors.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement error in errors.EnumerateArray())
                {
                    if (error.ValueKind == JsonValueKind.Object)
                    {
                        // Extract error message
                        string message = error.TryGetProperty("message", out JsonElement msg)
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
            if (root.TryGetProperty("data", out JsonElement data) && data.ValueKind != JsonValueKind.Null)
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

    private async Task CheckAndRecycleSessionAsync()
    {
        if (_sessionOrchestrator == null || _currentSessionId == null)
        {
            return;
        }

        try
        {
            SessionHealthMetrics health = await _sessionOrchestrator.GetSessionHealthAsync(_currentSessionId, default).ConfigureAwait(false);
            if (health.Health == SessionHealth.Unhealthy)
            {
                if (_logger != null) LogSessionRecycled(_logger, _currentSessionId, null);
                await _sessionOrchestrator.RecycleSessionAsync(_currentSessionId, default).ConfigureAwait(false);
                _currentSessionId = null;
            }
        }
        catch (Exception ex)
        {
            if (_logger != null) LogSessionHealthCheckFailed(_logger, _currentSessionId ?? "unknown", ex);
        }
    }
}

// lightweight JsonDocument builder placeholder for potential extension (keeps code flexible)
internal sealed class JsonDocumentBuilder : IDisposable
{
    public void Dispose() { }
}
