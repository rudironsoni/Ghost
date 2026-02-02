using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Ghost.Http;
using Ghost.Platform.Common.Session;
using Polly;

namespace Ghost.Platform.Glassdoor.Internal;

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
        _http = http ?? throw new ArgumentNullException(nameof(http));
        _sessionOrchestrator = null;
        _logger = logger;
        _retryPolicy = EnhancedRetryPolicy.CreatePolicy(logger, maxRetries: 4, enableJitter: true);
    }

    /// <summary>
    /// Modern constructor with SessionOrchestrator support for session continuity and health monitoring.
    /// </summary>
    public GlassdoorApiClient(ISessionOrchestrator sessionOrchestrator, ILogger<GlassdoorApiClient>? logger = null)
    {
        _http = null;
        _sessionOrchestrator = sessionOrchestrator ?? throw new ArgumentNullException(nameof(sessionOrchestrator));
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

            if (_currentSessionId != null && _sessionOrchestrator != null)
            {
                try
                {
                    _sessionOrchestrator.CloseSessionAsync(_currentSessionId, default).GetAwaiter().GetResult();
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
            return await GetCsrfTokenWithOrchestratorAsync(ct);
        }
        else
        {
            return await GetCsrfTokenLegacyAsync(ct);
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

            var sessionId = await _sessionOrchestrator!.AllocateSessionAsync(context, ct);
            
            try
            {
                var httpSession = await _sessionOrchestrator.GetHttpSessionAsync(sessionId, ct);
                if (httpSession == null)
                {
                    LogTokenExtraction($"Failed to get HTTP session {sessionId}");
                    return GlassdoorConstants.FallbackToken;
                }

                var request = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm?loc=US");
                request.Headers.Host = "www.glassdoor.com";
                foreach (var header in GlassdoorConstants.CsrfHeaders)
                {
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                var res = await _retryPolicy.ExecuteAsync(async () => 
                    await httpSession.ExecuteAsync(() => request, ct).ConfigureAwait(false)).ConfigureAwait(false);
                var html = await res.Content.ReadAsStringAsync(ct);

                try { System.IO.File.WriteAllText("logs/glassdoor_csrf.html", html); } catch { }

                LogTokenExtraction($"Received HTML response: {html.Length} characters");

                if (IsConsentOrBlockedPage(html))
                {
                    LogTokenExtraction("Detected consent or blocked page, trying alternative approach");

                    var altRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm");
                    foreach (var header in GlassdoorConstants.AlternativeHeaders)
                    {
                        altRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    var altRes = await _retryPolicy.ExecuteAsync(async () => 
                        await httpSession.ExecuteAsync(() => altRequest, ct).ConfigureAwait(false)).ConfigureAwait(false);
                    html = await altRes.Content.ReadAsStringAsync(ct);

                    try { System.IO.File.WriteAllText("logs/glassdoor_csrf_alt.html", html); } catch { }

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

                    var isValid = await ValidateTokenWithOrchestratorAsync(token, httpSession, ct);
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
                await _sessionOrchestrator.CloseSessionAsync(sessionId, ct);
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
            foreach (var header in GlassdoorConstants.CsrfHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            var res = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(request, ct).ConfigureAwait(false)).ConfigureAwait(false);
            var html = await res.Content.ReadAsStringAsync(ct);

            // DEBUG: Write raw HTML to file
            try { System.IO.File.WriteAllText("logs/glassdoor_csrf.html", html); } catch { }

            LogTokenExtraction($"Received HTML response: {html.Length} characters");

            // Check for consent/blocking pages
            if (IsConsentOrBlockedPage(html))
            {
                LogTokenExtraction("Detected consent or blocked page, trying alternative approach");

                // Try alternative approach with different headers
                var altRequest = new HttpRequestMessage(HttpMethod.Get, "https://www.glassdoor.com/index.htm");
                foreach (var header in GlassdoorConstants.AlternativeHeaders)
                {
                    altRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                var altRes = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(altRequest, ct).ConfigureAwait(false)).ConfigureAwait(false);
                html = await altRes.Content.ReadAsStringAsync(ct);

                try { System.IO.File.WriteAllText("logs/glassdoor_csrf_alt.html", html); } catch { }

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
                var isValid = await ValidateTokenAsync(token, ct);
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
            var logMessage = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] {message}\n";
            System.IO.File.AppendAllText("logs/glassdoor_token_extraction.log", logMessage);
        }
        catch { }
    }

    /// <summary>
    /// Validate extracted token by testing it against the API
    /// </summary>
    private async Task<bool> ValidateTokenWithOrchestratorAsync(string token, RotatingProxySession httpSession, CancellationToken ct)
    {
        try
        {
            LogTokenExtraction($"Validating token: {token.Substring(0, Math.Min(10, token.Length))}...");

            var testPayload = JsonSerializer.Serialize(new[]
            {
                new
                {
                    operationName = "JobSearchResultsQuery",
                    variables = new
                    {
                        excludeJobListingIds = new List<int>(),
                        filterParams = new List<object>(),
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

            foreach (var header in GlassdoorConstants.GraphHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Headers.TryAddWithoutValidation("gd-csrf-token", token);

            var res = await _retryPolicy.ExecuteAsync(async () => 
                await httpSession.ExecuteAsync(() => request, ct).ConfigureAwait(false)).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync(ct);

            var (hasErrors, shouldRetry) = ParseGraphQLErrors(json);

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
            var testPayload = JsonSerializer.Serialize(new[]
            {
                new
                {
                    operationName = "JobSearchResultsQuery",
                    variables = new
                    {
                        excludeJobListingIds = new List<int>(),
                        filterParams = new List<object>(),
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

            foreach (var header in GlassdoorConstants.GraphHeaders)
            {
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            request.Headers.TryAddWithoutValidation("gd-csrf-token", token);

            var res = await _retryPolicy.ExecuteAsync(async () => await _http!.SendAsync(request, ct).ConfigureAwait(false)).ConfigureAwait(false);
            var json = await res.Content.ReadAsStringAsync(ct);

            // Check if response is valid (not an auth error)
            var (hasErrors, shouldRetry) = ParseGraphQLErrors(json);

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

        // Enhanced fallback patterns for different HTML structures
        var fallbackPatterns = new[]
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

        foreach (var pattern in fallbackPatterns)
        {
            match = Regex.Match(html, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success && match.Groups.Count > 1)
            {
                var token = match.Groups[1].Value;
                if (!string.IsNullOrEmpty(token) && token.Length > 10)
                {
                    // Try to extract token from JSON content if it's a JSON object
                    var extractedToken = ExtractTokenFromJsonContent(token);
                    if (!string.IsNullOrEmpty(extractedToken))
                    {
                        return extractedToken;
                    }
                    return token;
                }
            }
        }

        // JSON-based extraction: Parse all JSON script tags and search recursively
        var jsonScriptPattern = @"<script[^>]*type\s*=\s*[""']application/json[""'][^>]*>(.*?)</script>";
        var jsonMatches = Regex.Matches(html, jsonScriptPattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        foreach (Match jsonMatch in jsonMatches)
        {
            var jsonContent = jsonMatch.Groups[1].Value;
            var token = ExtractTokenFromJsonRecursively(jsonContent);
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
            var value = element.GetString();
            if (!string.IsNullOrEmpty(value) && value.Length > 10 && (value.Contains("csrf") || value.Contains("token")))
            {
                return value;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var propertyName = property.Name.ToLowerInvariant();
                if (propertyName.Contains("token") || propertyName.Contains("csrf"))
                {
                    var token = FindTokenInJsonElement(property.Value);
                    if (!string.IsNullOrEmpty(token))
                    {
                        return token;
                    }
                }
                else
                {
                    var token = FindTokenInJsonElement(property.Value);
                    if (!string.IsNullOrEmpty(token))
                    {
                        return token;
                    }
                }
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var token = FindTokenInJsonElement(item);
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
            var tokenPatterns = new[]
            {
                "\"token\"\\s*:\\s*\"([^\"]+)\"",
                "\"csrf\"\\s*:\\s*\"([^\"]+)\"",
                "\"gd-csrf-token\"\\s*:\\s*\"([^\"]+)\""
            };

            foreach (var pattern in tokenPatterns)
            {
                var match = Regex.Match(jsonContent, pattern, RegexOptions.IgnoreCase);
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
        var defaultLocationId = 11047;
        var defaultLocationType = "STATE";

        var resolvedLocationId = defaultLocationId;
        var resolvedLocationType = defaultLocationType;

        if (!string.IsNullOrWhiteSpace(location))
        {
            // Normalize
            var loc = location.Trim().ToLowerInvariant();

            // Basic mapping for common locations. These IDs are best-effort and
            // may need refinement; they are chosen to change the search scope
            // (country vs state) so results differ from the default remote ID.
            switch (loc)
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
                case var s when s.StartsWith("province:", StringComparison.Ordinal) || s.StartsWith("state:", StringComparison.Ordinal):
                    // Allow callers to pass "state:11047" or "province:5" to force an id
                    var parts = s.Split(':', 2);
                    if (parts.Length == 2 && int.TryParse(parts[1], out var parsedId))
                    {
                        resolvedLocationId = parsedId;
                        resolvedLocationType = s.StartsWith("province:", StringComparison.Ordinal) ? "PROVINCE" : "STATE";
                    }
                    break;
                default:
                    // Unknown free-text location: leave fallback but log the value
                    try { System.IO.File.AppendAllText("logs/glassdoor_location_resolve.log", $"Unknown location '{location}' - falling back to default ({defaultLocationId})\n"); } catch { }
                    break;
            }
        }
        else
        {
            // No location provided - log that we're using default remote
            try { System.IO.File.AppendAllText("logs/glassdoor_location_resolve.log", $"No location provided - using default ({defaultLocationId})\n"); } catch { }
        }

        // Also log the final resolved mapping for transparency
        try { System.IO.File.AppendAllText("logs/glassdoor_location_resolve.log", $"Resolved location '{location}' => id={resolvedLocationId}, type={resolvedLocationType}\n"); } catch { }

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
        if (_sessionOrchestrator != null)
        {
            return await SearchWithOrchestratorAsync(keyword, location, csrfToken, ct);
        }
        else
        {
            return await SearchLegacyAsync(keyword, location, csrfToken, ct);
        }
    }

    private async Task<string?> SearchWithOrchestratorAsync(string keyword, string? location, string? csrfToken, CancellationToken ct)
    {
        var affinityKey = $"glassdoor_{keyword}_{location}_{Guid.NewGuid():N}";

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

            _currentSessionId = await _sessionOrchestrator!.AllocateSessionWithAffinityAsync(context, affinityOptions, ct);
            if (_logger != null) LogSessionAllocated(_logger, _currentSessionId, null);

            var httpSession = await _sessionOrchestrator.GetHttpSessionAsync(_currentSessionId, ct);
            if (httpSession == null)
            {
                if (_logger != null) LogSessionGetFailed(_logger, _currentSessionId, null);
                return null;
            }

            var token = csrfToken ?? await GetCsrfTokenWithOrchestratorAsync(ct);

            var payload = BuildSearchPayload(keyword, location);

            await ApplyRateLimitAsync(ct);

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

            try
            {
                var res = await _retryPolicy.ExecuteAsync(async () =>
                {
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

                    return await httpSession.ExecuteAsync(() => retryRequest, ct).ConfigureAwait(false);
                }).ConfigureAwait(false);

                var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

                try { System.IO.File.WriteAllText($"logs/glassdoor_search.json", json); } catch { }

                var (hasErrors, shouldRetry) = ParseGraphQLErrors(json);

                if (hasErrors)
                {
                    await CheckAndRecycleSessionAsync();
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
                await CheckAndRecycleSessionAsync();
                return null;
            }
        }
        finally
        {
            if (_currentSessionId != null)
            {
                await _sessionOrchestrator!.CloseSessionAsync(_currentSessionId, ct);
                _currentSessionId = null;
            }
        }
    }

    private async Task<string?> SearchLegacyAsync(string keyword, string? location, string? csrfToken, CancellationToken ct)
    {
        var token = csrfToken ?? await GetCsrfTokenAsync(ct);

        // Build payload based on JobSpy's structure
        var payload = BuildSearchPayload(keyword, location);

        // Apply rate limiting before the request
        await ApplyRateLimitAsync(ct);

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

        try
        {
            // Use EnhancedRetryPolicy for automatic retry with exponential backoff
            var res = await _retryPolicy.ExecuteAsync(async () =>
            {
                // Create a new request for each retry attempt
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

                return await _http!.SendAsync(retryRequest, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            var json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            // DEBUG: Write raw JSON to file
            try { System.IO.File.WriteAllText($"logs/glassdoor_search.json", json); } catch { }

            // Parse GraphQL response for errors
            var (hasErrors, shouldRetry) = ParseGraphQLErrors(json);

            if (hasErrors)
            {
                // EnhancedRetryPolicy handles retries for transient errors
                // If we still have errors after retries, return null
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
            // Operation was cancelled
            return null;
        }
        catch (Exception ex)
        {
            // Log the exception if logger is available
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

            // Handle case where root is an array (not a GraphQL response)
            if (root.ValueKind == JsonValueKind.Array)
            {
                return (false, false); // Assume success for array responses
            }

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

    private async Task CheckAndRecycleSessionAsync()
    {
        if (_sessionOrchestrator == null || _currentSessionId == null)
        {
            return;
        }

        try
        {
            var health = await _sessionOrchestrator.GetSessionHealthAsync(_currentSessionId, default);
            if (health.Health == SessionHealth.Unhealthy)
            {
                if (_logger != null) LogSessionRecycled(_logger, _currentSessionId, null);
                await _sessionOrchestrator.RecycleSessionAsync(_currentSessionId, default);
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
