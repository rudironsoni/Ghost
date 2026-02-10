using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Ghost.Abstractions;
using Ghost.Http;
using Ghost.Models;
using Ghost.Platform.Common.Session;
using Microsoft.Extensions.Logging;

namespace Ghost.Platform.Indeed.Internal;

public class IndeedApiClient : IDisposable
{
    private readonly IProxyProvider? _proxyProvider;
    private readonly ISessionOrchestrator? _sessionOrchestrator;
    private readonly CountryCode _country;
    private readonly string _apiKey;
    private readonly string _apiEndpoint;
    private readonly ILogger<IndeedApiClient> _logger;
    private readonly HttpClient _httpClient;
    private readonly SocketsHttpHandler _handler;
    private readonly IReadOnlyDictionary<string, string> _baseHeaders;
    private readonly string? _contentTypeHeader;
    private readonly object _metricsLock = new();
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _rateLimitSemaphore = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;
    private readonly TimeSpan _rateLimitDelay = TimeSpan.FromSeconds(2);
    private bool _disposed;
    private string? _currentSessionId;
    private long _activeRequests;
    private long _totalRequests;
    private long _totalFailures;
    private long _totalResponseTicks;
    private long _windowStartTicks;
    private long _windowRequestCount;
    private static readonly HashSet<string> ContentHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "content-type"
    };
    private static readonly TimeSpan MetricsWindow = TimeSpan.FromSeconds(1);
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
        : this(proxyProvider, null, options, logger, null, TimeProvider.System)
    {
    }

    /// <summary>
    /// Legacy constructor for DI using proxy provider and session orchestrator when available.
    /// </summary>
    public IndeedApiClient(IProxyProvider proxyProvider, ISessionOrchestrator sessionOrchestrator, IndeedOptions options, ILogger<IndeedApiClient> logger)
        : this(proxyProvider, sessionOrchestrator, options, logger, null, TimeProvider.System)
    {
    }

    /// <summary>
    /// Modern constructor with SessionOrchestrator support for session continuity and health monitoring.
    /// </summary>
    public IndeedApiClient(ISessionOrchestrator sessionOrchestrator, IndeedOptions options, ILogger<IndeedApiClient> logger)
        : this(null, sessionOrchestrator, options, logger, null, TimeProvider.System)
    {
    }

    internal IndeedApiClient(
        IProxyProvider? proxyProvider,
        ISessionOrchestrator? sessionOrchestrator,
        IndeedOptions options,
        ILogger<IndeedApiClient> logger,
        HttpMessageHandler? handler,
        TimeProvider timeProvider)
    {
        _proxyProvider = proxyProvider;
        _sessionOrchestrator = sessionOrchestrator;
        _country = options.Country;
        _apiKey = options.ApiKey;
        _apiEndpoint = options.ApiEndpoint;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;

        _baseHeaders = BuildBaseHeaders(_country, _apiKey, out var contentTypeHeader);
        _contentTypeHeader = contentTypeHeader;

        if (handler is SocketsHttpHandler socketsHandler)
        {
            _handler = socketsHandler;
            _httpClient = CreateHttpClient(_handler);
        }
        else if (handler != null)
        {
            _handler = CreateSocketsHttpHandler();
            _httpClient = new HttpClient(handler, disposeHandler: false)
            {
                Timeout = TimeSpan.FromSeconds(30),
                DefaultRequestVersion = HttpVersion.Version20,
                DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
            };
            ApplyDefaultHeaders(_httpClient, _baseHeaders);
        }
        else
        {
            _handler = CreateSocketsHttpHandler();
            _httpClient = CreateHttpClient(_handler);
        }

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

    public IndeedConnectionMetrics GetMetrics()
    {
        lock (_metricsLock)
        {
            var nowTicks = _timeProvider.GetUtcNow().Ticks;
            var windowTicks = nowTicks - _windowStartTicks;
            var elapsedSeconds = Math.Max(TimeSpan.FromTicks(windowTicks).TotalSeconds, 1d);
            var requestsPerSecond = _windowRequestCount / elapsedSeconds;
            var averageResponseMs = _totalRequests > 0
                ? TimeSpan.FromTicks(_totalResponseTicks / _totalRequests).TotalMilliseconds
                : 0d;

            return new IndeedConnectionMetrics(
                ActiveConnections: _activeRequests,
                RequestsPerSecond: requestsPerSecond,
                AverageResponseTimeMs: averageResponseMs,
                TotalRequests: _totalRequests,
                TotalFailures: _totalFailures);
        }
    }

    private Dictionary<string, string> BuildBaseHeaders(CountryCode country, string apiKey, out string? contentTypeHeader)
    {
        Dictionary<string, string> headers;
        try
        {
            headers = IndeedConstants.GetHeaders(country, apiKey);
        }
        catch (ArgumentException ex)
        {
            LogGetHeadersReturnedNull(_logger, country, ex);
            if (!string.IsNullOrEmpty(apiKey))
            {
                headers = IndeedConstants.GetHeaders(CountryCode.US, apiKey);
            }
            else
            {
                throw;
            }
        }

        contentTypeHeader = null;
        foreach (var kv in headers)
        {
            if (ContentHeaderNames.Contains(kv.Key))
            {
                contentTypeHeader = kv.Value;
                break;
            }
        }

        return headers;
    }

    private SocketsHttpHandler CreateSocketsHttpHandler()
    {
        var handler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 100,
            EnableMultipleHttp2Connections = true,
            AutomaticDecompression = DecompressionMethods.All,
            UseCookies = false,
            UseProxy = _proxyProvider != null,
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                RemoteCertificateValidationCallback = HttpClientSecurityExtensions.CreateCertificateValidationCallback()
            }
        };

        if (_proxyProvider != null)
        {
            handler.Proxy = new RotatingWebProxy(_proxyProvider);
        }

        return handler;
    }

    private HttpClient CreateHttpClient(SocketsHttpHandler handler)
    {
        var client = new HttpClient(handler, disposeHandler: false)
        {
            Timeout = TimeSpan.FromSeconds(30),
            DefaultRequestVersion = HttpVersion.Version20,
            DefaultVersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
        };

        ApplyDefaultHeaders(client, _baseHeaders);
        return client;
    }

    private static void ApplyDefaultHeaders(HttpClient client, IReadOnlyDictionary<string, string> headers)
    {
        foreach (var kv in headers)
        {
            if (ContentHeaderNames.Contains(kv.Key))
            {
                continue;
            }

            client.DefaultRequestHeaders.TryAddWithoutValidation(kv.Key, kv.Value);
        }
    }

    internal HttpRequestMessage CreateRequest(object payload)
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

        foreach (var kv in _baseHeaders)
        {
            if (ContentHeaderNames.Contains(kv.Key))
            {
                continue;
            }

            req.Headers.TryAddWithoutValidation(kv.Key, kv.Value);
        }

        return req;
    }

    private void StartRequestMetrics(out Stopwatch stopwatch)
    {
        lock (_metricsLock)
        {
            if (_windowStartTicks == 0)
            {
                _windowStartTicks = _timeProvider.GetUtcNow().Ticks;
            }

            var elapsed = _timeProvider.GetUtcNow().Ticks - _windowStartTicks;
            if (elapsed > MetricsWindow.Ticks)
            {
                _windowStartTicks = _timeProvider.GetUtcNow().Ticks;
                _windowRequestCount = 0;
            }
        }

        Interlocked.Increment(ref _activeRequests);
        stopwatch = Stopwatch.StartNew();
    }

    private void EndRequestMetrics(bool success, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        Interlocked.Decrement(ref _activeRequests);
        lock (_metricsLock)
        {
            _totalRequests++;
            if (!success)
            {
                _totalFailures++;
            }

            _totalResponseTicks += stopwatch.ElapsedTicks;

            _windowRequestCount++;
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

    public async Task<JsonElement> SearchPageAsync(string query, string location, int limit, string? cursor, CancellationToken ct)
    {
        if (_sessionOrchestrator != null)
        {
            return await SearchPageWithOrchestratorAsync(query, location, limit, cursor, ct).ConfigureAwait(false);
        }

        return await SearchPageLegacyAsync(query, location, limit, cursor, ct).ConfigureAwait(false);
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

                LogSendingRequest(_logger, _apiEndpoint, null);

                foreach (var header in _httpClient.DefaultRequestHeaders)
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
                bool success = false;
                Stopwatch stopwatch;
                StartRequestMetrics(out stopwatch);

                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        using var req = CreateRequest(payload);
                        foreach (var header in req.Headers)
                        {
                            LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
                        }
                        if (req.Content?.Headers != null)
                        {
                            foreach (var header in req.Content.Headers)
                            {
                                LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
                            }
                        }
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

                EndRequestMetrics(success && resp != null && resp.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content), stopwatch);

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

            LogSendingRequest(_logger, _apiEndpoint, null);

            foreach (var header in _httpClient.DefaultRequestHeaders)
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
            Stopwatch stopwatch;
            StartRequestMetrics(out stopwatch);

            for (int attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    using var attemptReq = CreateRequest(payload);
                    foreach (var header in attemptReq.Headers)
                    {
                        LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
                    }
                    if (attemptReq.Content?.Headers != null)
                    {
                        foreach (var header in attemptReq.Content.Headers)
                        {
                            LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
                        }
                    }
                    resp = await _httpClient.SendAsync(attemptReq);

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

            EndRequestMetrics(resp != null && resp.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content), stopwatch);

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

            try
            {
                _httpClient?.Dispose();
            }
            catch { }

            try
            {
                _handler?.Dispose();
            }
            catch { }

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

    private async Task<JsonElement> SearchPageWithOrchestratorAsync(string query, string location, int limit, string? cursor, CancellationToken ct)
    {
        await EnsureSessionAsync(query, location, ct).ConfigureAwait(false);
        var httpSession = await _sessionOrchestrator!.GetHttpSessionAsync(_currentSessionId!, ct).ConfigureAwait(false);
        if (httpSession is null)
        {
            LogSessionGetFailed(_logger, _currentSessionId ?? "unknown", null);
            return default;
        }

        await ApplyRateLimitAsync(ct).ConfigureAwait(false);
        var formattedQuery = string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, limit));
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            formattedQuery += $" after: \"{cursor}\"";
        }

        var payload = new { query = formattedQuery };
        string content = string.Empty;
        bool success = false;
        HttpResponseMessage? resp = null;
        Stopwatch stopwatch;
        StartRequestMetrics(out stopwatch);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var req = CreateRequest(payload);
                resp = await httpSession.ExecuteAsync(() => req, ct).ConfigureAwait(false);
                if ((int)resp.StatusCode == 429)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), ct).ConfigureAwait(false);
                    continue;
                }

                content = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (IsBlockedOrConsentRequired(content))
                {
                    if (attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 2000), ct).ConfigureAwait(false);
                        continue;
                    }
                    break;
                }

                success = true;
                break;
            }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), ct).ConfigureAwait(false);
            }
        }

        EndRequestMetrics(success && resp != null && resp.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content), stopwatch);
        if (!success || resp == null || !resp.IsSuccessStatusCode || IsBlockedOrConsentRequired(content))
        {
            await CheckAndRecycleSessionAsync().ConfigureAwait(false);
            return default;
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    private async Task<JsonElement> SearchPageLegacyAsync(string query, string location, int limit, string? cursor, CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct).ConfigureAwait(false);
        var formattedQuery = string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, limit));
        if (!string.IsNullOrWhiteSpace(cursor))
        {
            formattedQuery += $" after: \"{cursor}\"";
        }

        var payload = new { query = formattedQuery };
        string content = string.Empty;
        HttpResponseMessage? resp = null;
        Stopwatch stopwatch;
        StartRequestMetrics(out stopwatch);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                using var req = CreateRequest(payload);
                resp = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
                if ((int)resp.StatusCode == 429)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), ct).ConfigureAwait(false);
                    continue;
                }

                content = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                if (IsBlockedOrConsentRequired(content))
                {
                    if (attempt < 2)
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 2000), ct).ConfigureAwait(false);
                        continue;
                    }
                    break;
                }

                break;
            }
            catch (HttpRequestException) when (attempt < 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 1000), ct).ConfigureAwait(false);
            }
        }

        EndRequestMetrics(resp != null && resp.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content), stopwatch);
        if (resp == null || !resp.IsSuccessStatusCode || IsBlockedOrConsentRequired(content))
        {
            return default;
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    private async Task EnsureSessionAsync(string query, string location, CancellationToken ct)
    {
        if (_sessionOrchestrator == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(_currentSessionId))
        {
            return;
        }

        var affinityKey = $"indeed_{query}_{location}_{Guid.NewGuid():N}";
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

        _currentSessionId = await _sessionOrchestrator.AllocateSessionWithAffinityAsync(context, affinityOptions, ct).ConfigureAwait(false);
        LogSessionAllocated(_logger, _currentSessionId, null);
    }
}

public sealed record IndeedConnectionMetrics(
    long ActiveConnections,
    double RequestsPerSecond,
    double AverageResponseTimeMs,
    long TotalRequests,
    long TotalFailures);
