using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Authentication;
using Ghost.Models;
using Ghost.Abstractions;
using Ghost.Http;
using Ghost.Platform.Common.Session;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.Indeed.Internal;

public class IndeedApiClient : IDisposable
{
    private readonly IProxyProvider? _proxyProvider;
    private readonly ISessionOrchestrator? _sessionOrchestrator;
    private readonly CountryCode _country;
    private readonly string _apiKey;
    private readonly ILogger<IndeedApiClient> _logger;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(2);
    private bool _disposed;
    private string? _currentSessionId;
        private static readonly Action<ILogger, string, string, Exception?> LogRequestStart =
            LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2001, "FetchingIndeedJobs"), "Fetching Indeed jobs for query '{Query}' at {Location}...");
        private static readonly Action<ILogger, string, Exception?> LogSendingRequest =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2002, "SendingRequest"), "Sending request to {Url}");
        private static readonly Action<ILogger, string, Exception?> LogResponseStatus =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2003, "ResponseStatus"), "Response Status: {StatusCode}");
        private static readonly Action<ILogger, string, Exception?> LogResponseContent =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2004, "ResponseContent"), "Response Content: {Content}");
        private static readonly Action<ILogger, string, Exception?> LogRequestPayload =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2005, "RequestPayload"), "Request Payload: {Content}");
        private static readonly Action<ILogger, string, string, Exception?> LogRequestHeader =
            LoggerMessage.Define<string, string>(LogLevel.Information, new EventId(2006, nameof(LogRequestHeader)), "Header: {Key} = {Value}");

        private static readonly Action<ILogger, CountryCode, Exception?> LogConstructedWithCountry =
            LoggerMessage.Define<CountryCode>(LogLevel.Information, new EventId(2007, "ConstructedWithCountry"), "IndeedApiClient constructed with Country={Country}");

        private static readonly Action<ILogger, CountryCode, Exception?> LogGetHeadersReturnedNull =
            LoggerMessage.Define<CountryCode>(LogLevel.Warning, new EventId(2008, "GetHeadersReturnedNull"), "IndeedConstants.GetHeaders returned null for country {Country}");

        private static readonly Action<ILogger, CountryCode, Exception?> LogUsingCountryForRequest =
            LoggerMessage.Define<CountryCode>(LogLevel.Information, new EventId(2009, "UsingCountryForRequest"), "IndeedApiClient: using country {Country} when sending request");

        private static readonly Action<ILogger, string, Exception?> LogSessionAllocated =
            LoggerMessage.Define<string>(LogLevel.Information, new EventId(2010, "SessionAllocated"), "Allocated session {SessionId} for Indeed requests");

        private static readonly Action<ILogger, string, Exception?> LogSessionRecycled =
            LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2011, "SessionRecycled"), "Recycling unhealthy session {SessionId}");

        private static readonly Action<ILogger, string, Exception?> LogSessionGetFailed =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(2012, "SessionGetFailed"), "Failed to get HTTP session {SessionId}");

        private static readonly Action<ILogger, string, Exception?> LogSessionHealthCheckFailed =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(2013, "SessionHealthCheckFailed"), "Failed to check session health for {SessionId}");

        private static readonly Action<ILogger, string, Exception?> LogSessionCloseFailed =
            LoggerMessage.Define<string>(LogLevel.Error, new EventId(2014, "SessionCloseFailed"), "Failed to close session {SessionId} during disposal");

        private static readonly CompositeFormat JobSearchQueryFormat = CompositeFormat.Parse(IndeedConstants.JobSearchQuery);

    /// <summary>
    /// Legacy constructor for backward compatibility. Uses direct proxy provider.
    /// </summary>
    public IndeedApiClient(Ghost.Abstractions.IProxyProvider proxyProvider, IndeedOptions options, ILogger<IndeedApiClient> logger)
    {
        _proxyProvider = proxyProvider ?? throw new ArgumentNullException(nameof(proxyProvider));
        _sessionOrchestrator = null;
        _country = options.Country;
        _apiKey = options.ApiKey;
        _logger = logger;
        try
        {
            LogConstructedWithCountry(_logger, _country, null);
        }
        catch { }
    }

    /// <summary>
    /// Modern constructor with SessionOrchestrator support for session continuity and health monitoring.
    /// </summary>
    public IndeedApiClient(ISessionOrchestrator sessionOrchestrator, IndeedOptions options, ILogger<IndeedApiClient> logger)
    {
        _proxyProvider = null;
        _sessionOrchestrator = sessionOrchestrator ?? throw new ArgumentNullException(nameof(sessionOrchestrator));
        _country = options.Country;
        _apiKey = options.ApiKey;
        _logger = logger;
        try
        {
            LogConstructedWithCountry(_logger, _country, null);
        }
        catch { }
    }

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

    public async IAsyncEnumerable<JsonElement> SearchAsync(string query, string location, int limit = 50)
    {
        if (_sessionOrchestrator != null)
        {
            await foreach (var result in SearchWithOrchestratorAsync(query, location, limit))
            {
                yield return result;
            }
        }
        else
        {
            await foreach (var result in SearchLegacyAsync(query, location, limit))
            {
                yield return result;
            }
        }
    }

    private async IAsyncEnumerable<JsonElement> SearchWithOrchestratorAsync(string query, string location, int limit)
    {
        string? cursor = null;
        int remaining = limit;
        var affinityKey = $"indeed_{query}_{location}_{Guid.NewGuid():N}";

        LogRequestStart(_logger, query, location, null);

        try
        {
            var context = new SessionAllocationContext(
                PlatformName: "Indeed",
                CountryCode: _country.ToString(),
                SessionType: SessionType.Http,
                ComplexityScore: 30,
                Metadata: new Dictionary<string, string>
                {
                    ["Query"] = query,
                    ["Location"] = location
                }
            );

            var affinityOptions = new SessionAffinityOptions(
                AffinityKey: affinityKey,
                AffinityDuration: TimeSpan.FromMinutes(5),
                AllowFallback: true
            );

            _currentSessionId = await _sessionOrchestrator!.AllocateSessionWithAffinityAsync(context, affinityOptions, default);
            LogSessionAllocated(_logger, _currentSessionId, null);

            var httpSession = await _sessionOrchestrator.GetHttpSessionAsync(_currentSessionId, default);
            if (httpSession == null)
            {
                LogSessionGetFailed(_logger, _currentSessionId, null);
                yield break;
            }

            do
            {
                await ApplyRateLimitAsync(default);

                var formattedQuery = string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, remaining));
                var payload = new { query = formattedQuery };
                var json = JsonSerializer.Serialize(payload);
                LogRequestPayload(_logger, json, null);

                using var req = new HttpRequestMessage(HttpMethod.Post, IndeedConstants.ApiUrl)
                {
                    Content = JsonContent.Create(payload)
                };

                if (req.Content != null && !req.Content.Headers.Contains("Content-Type"))
                {
                    req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                }

                LogSendingRequest(_logger, IndeedConstants.ApiUrl, null);

                Dictionary<string, string> headers;
                try
                {
                    headers = IndeedConstants.GetHeaders(_country, _apiKey);
                }
                catch (ArgumentException ex)
                {
                    LogGetHeadersReturnedNull(_logger, _country, ex);
                    if (!string.IsNullOrEmpty(_apiKey))
                    {
                        headers = IndeedConstants.GetHeaders(CountryCode.US, _apiKey);
                    }
                    else
                    {
                        throw;
                    }
                }

                foreach (var kv in headers)
                {
                    req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
                }

                try
                {
                    LogUsingCountryForRequest(_logger, _country, null);
                }
                catch { }

                HttpResponseMessage? resp = null;
                string content = string.Empty;
                bool success = false;

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        resp = await httpSession.ExecuteAsync(() => req, default);

                        if ((int)resp.StatusCode == 429)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000));
                            continue;
                        }

                        LogResponseStatus(_logger, resp.StatusCode.ToString(), null);

                        content = await resp.Content.ReadAsStringAsync();
                        LogResponseContent(_logger, content, null);

                        try { System.IO.File.WriteAllText($"logs/indeed_jobs_search_{attempt}.json", content); } catch { }

                        resp.EnsureSuccessStatusCode();

                        if (IsBlockedOrConsentRequired(content))
                        {
                            if (attempt < 2)
                            {
                                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 2000));
                                continue;
                            }
                            break;
                        }

                        success = true;
                        break;
                    }
                    catch (HttpRequestException) when (attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000));
                        continue;
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }

                if (!success || resp == null || !resp.IsSuccessStatusCode || IsBlockedOrConsentRequired(content))
                {
                    await CheckAndRecycleSessionAsync();
                    break;
                }

                using var doc = JsonDocument.Parse(content);
                if (doc is null) yield break;

                yield return doc.RootElement.Clone();

                if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("jobSearch", out var jobSearch) || !jobSearch.TryGetProperty("pageInfo", out var pageInfo) || !pageInfo.TryGetProperty("nextCursor", out var nextCursorEl))
                {
                    break;
                }

                cursor = nextCursorEl.GetString();
                if (string.IsNullOrEmpty(cursor)) break;
                remaining -= 25;
            }
            while (remaining > 0);
        }
        finally
        {
            if (_currentSessionId != null)
            {
                await _sessionOrchestrator!.CloseSessionAsync(_currentSessionId, default);
                _currentSessionId = null;
            }
        }
    }

    private async IAsyncEnumerable<JsonElement> SearchLegacyAsync(string query, string location, int limit)
    {
        string? cursor = null;
        int remaining = limit;

        LogRequestStart(_logger, query, location, null);

        do
        {
            await ApplyRateLimitAsync(default);

            var formattedQuery = string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, remaining));
            var payload = new { query = formattedQuery };
            var json = JsonSerializer.Serialize(payload);
            LogRequestPayload(_logger, json, null);

            using var req = new HttpRequestMessage(HttpMethod.Post, IndeedConstants.ApiUrl)
            {
                Content = JsonContent.Create(payload)
            };

            if (req.Content != null && !req.Content.Headers.Contains("Content-Type"))
            {
                req.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            }

            LogSendingRequest(_logger, IndeedConstants.ApiUrl, null);

            foreach (var header in req.Headers)
            {
                var value = string.Join(",", header.Value);
                LogRequestHeader(_logger, header.Key, value, null);
            }

            var proxy = await _proxyProvider!.GetProxyAsync(_country.ToString());

            var handler = new SocketsHttpHandler
            {
                SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                {
                    RemoteCertificateValidationCallback = HttpClientSecurityExtensions.CreateCertificateValidationCallback(),
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13
                }
            };

            if (proxy != null)
            {
                var webProxy = new WebProxy(new Uri(proxy.Server));
                if (!string.IsNullOrEmpty(proxy.Username))
                {
                    webProxy.Credentials = new NetworkCredential(proxy.Username, proxy.Password);
                }
                handler.Proxy = webProxy;
                handler.UseProxy = true;
            }

            using var client = new HttpClient(handler);

            Dictionary<string, string> headers;
            try
            {
                headers = IndeedConstants.GetHeaders(_country, _apiKey);
            }
            catch (ArgumentException ex)
            {
                LogGetHeadersReturnedNull(_logger, _country, ex);
                if (!string.IsNullOrEmpty(_apiKey))
                {
                    headers = IndeedConstants.GetHeaders(CountryCode.US, _apiKey);
                }
                else
                {
                    throw;
                }
            }

            foreach (var kv in headers)
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
            }

            foreach (var header in client.DefaultRequestHeaders)
            {
                LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
            }

            foreach (var header in req.Headers)
            {
                LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
            }

            try
            {
                LogUsingCountryForRequest(_logger, _country, null);
            }
            catch { }

            HttpResponseMessage? resp = null;
            string content = string.Empty;

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    // Create a fresh request message for each attempt to avoid "already sent" error
                    var attemptPayload = new { query = formattedQuery };
                    var attemptReq = new HttpRequestMessage(HttpMethod.Post, IndeedConstants.ApiUrl)
                    {
                        Content = JsonContent.Create(attemptPayload)
                    };
                    
                    if (attemptReq.Content != null && !attemptReq.Content.Headers.Contains("Content-Type"))
                    {
                        attemptReq.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                    }
                    
                    // Add headers for each attempt
                    foreach (var header in headers)
                    {
                        attemptReq.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                    
                    resp = await client.SendAsync(attemptReq);

                    if ((int)resp.StatusCode == 429)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000));
                        continue;
                    }

                    LogResponseStatus(_logger, resp.StatusCode.ToString(), null);

                    content = await resp.Content.ReadAsStringAsync();
                    LogResponseContent(_logger, content, null);

                    try { System.IO.File.WriteAllText($"logs/indeed_jobs_search_{attempt}.json", content); } catch { }

                    resp.EnsureSuccessStatusCode();

                    if (IsBlockedOrConsentRequired(content))
                    {
                        if (attempt < 2)
                        {
                            await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 2000));
                            continue;
                        }
                        break;
                    }

                    break;
                }
                catch (HttpRequestException) when (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000));
                    continue;
                }
                catch (Exception)
                {
                    throw;
                }
            }

            if (resp == null || !resp.IsSuccessStatusCode || IsBlockedOrConsentRequired(content))
            {
                break;
            }

            using var doc = JsonDocument.Parse(content);
            if (doc is null) yield break;

            yield return doc.RootElement.Clone();

            if (!doc.RootElement.TryGetProperty("data", out var data) || !data.TryGetProperty("jobSearch", out var jobSearch) || !jobSearch.TryGetProperty("pageInfo", out var pageInfo) || !pageInfo.TryGetProperty("nextCursor", out var nextCursorEl))
            {
                break;
            }

            cursor = nextCursorEl.GetString();
            if (string.IsNullOrEmpty(cursor)) break;
            remaining -= 25;
        }
        while (remaining > 0);
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
                LogSessionRecycled(_logger, _currentSessionId, null);
                await _sessionOrchestrator.RecycleSessionAsync(_currentSessionId, default);
                _currentSessionId = null;
            }
        }
        catch (Exception ex)
        {
            LogSessionHealthCheckFailed(_logger, _currentSessionId ?? "unknown", ex);
        }
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
                    LogSessionCloseFailed(_logger, _currentSessionId, ex);
                }
            }

            _disposed = true;
        }
    }

    private static bool IsBlockedOrConsentRequired(string responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
            return true;

        // Only check for explicit error indicators at the start of the response
        // Valid job responses will contain {"data":{"jobSearch"...}}
        var trimmed = responseContent.TrimStart();
        
        // If it starts with valid JSON object with "data" property, it's likely a valid response
        if (trimmed.StartsWith("{\"data\":", StringComparison.Ordinal) ||
            trimmed.StartsWith("{\"data\": {", StringComparison.Ordinal))
        {
            return false;
        }

        // Check for explicit error page indicators (not job content)
        return trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
               trimmed.StartsWith('<') ||
               responseContent.Contains("\"errors\":", StringComparison.Ordinal) ||
               responseContent.Contains("access denied", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("rate limit exceeded", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("throttled", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("\"unauthorized\"", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("\"forbidden\"", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("blocked", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("g-recaptcha", StringComparison.OrdinalIgnoreCase) ||
               responseContent.Contains("cf_chl_jschl", StringComparison.OrdinalIgnoreCase);
    }
}
