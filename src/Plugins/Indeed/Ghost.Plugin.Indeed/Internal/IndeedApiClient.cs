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
using Ghost.Http;
using Ghost.Infrastructure.Session;
using Ghost.Models;
using Microsoft.Extensions.Logging;
using LoggerMessage = Microsoft.Extensions.Logging.LoggerMessage;

namespace Ghost.Plugin.Indeed.Internal;

public class IndeedApiClient : IAsyncDisposable, IDisposable
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

    private static readonly Action<ILogger, string, Exception?> LogLoggingFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2015, "LoggingFailed"), "Failed to log {Message}");

    private static readonly Action<ILogger, string, Exception?> LogDisposalFailed =
        LoggerMessage.Define<string>(LogLevel.Warning, new EventId(2016, "DisposalFailed"), "Failed to dispose {Resource}");

    private static readonly CompositeFormat JobSearchQueryFormat = CompositeFormat.Parse(IndeedConstants.JobSearchQuery);

    /// <summary>
    /// Legacy constructor for backward compatibility. Uses direct proxy provider.
    /// </summary>
    public IndeedApiClient(Ghost.IProxyProvider proxyProvider, IndeedOptions options, ILogger<IndeedApiClient> logger)
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

        _baseHeaders = BuildBaseHeaders(_country, _apiKey, out string? contentTypeHeader);
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
        catch (Exception ex)
        {
            LogLoggingFailed(_logger, "constructor country information", ex);
        }
    }

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

    public IndeedConnectionMetrics GetMetrics()
    {
        lock (_metricsLock)
        {
            long nowTicks = _timeProvider.GetUtcNow().Ticks;
            long windowTicks = nowTicks - _windowStartTicks;
            double elapsedSeconds = Math.Max(TimeSpan.FromTicks(windowTicks).TotalSeconds, 1d);
            double requestsPerSecond = _windowRequestCount / elapsedSeconds;
            double averageResponseMs = _totalRequests > 0
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
        foreach (KeyValuePair<string, string> kv in headers)
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

    private void StartRequestMetrics(out Stopwatch stopwatch)
    {
        lock (_metricsLock)
        {
            if (_windowStartTicks == 0)
            {
                _windowStartTicks = _timeProvider.GetUtcNow().Ticks;
            }

            long elapsed = _timeProvider.GetUtcNow().Ticks - _windowStartTicks;
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
            await foreach (JsonElement result in SearchWithOrchestratorAsync(query, location, limit).ConfigureAwait(false))
            {
                yield return result;
            }
        }
        else
        {
            await foreach (JsonElement result in SearchLegacyAsync(query, location, limit).ConfigureAwait(false))
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
        string affinityKey = $"indeed_{query}_{location}_{Guid.NewGuid():N}";

        LogRequestStart(_logger, query, location, null);

        try
        {
            RotatingProxySession? httpSession = await AllocateAndGetHttpSessionAsync(query, location, affinityKey).ConfigureAwait(false);
            if (httpSession is null)
            {
                yield break;
            }

            await foreach (JsonElement result in ExecuteSearchWithPaginationAsync(httpSession, query, location, cursor, remaining).ConfigureAwait(false))
            {
                yield return result;
            }
        }
        finally
        {
            await CloseCurrentSessionAsync().ConfigureAwait(false);
        }
    }

    private async Task<RotatingProxySession?> AllocateAndGetHttpSessionAsync(string query, string location, string affinityKey)
    {
        SessionAllocationContext context = CreateSessionAllocationContext(query, location);
        SessionAffinityOptions affinityOptions = new(
            AffinityKey: affinityKey,
            AffinityDuration: TimeSpan.FromMinutes(5),
            AllowFallback: true
        );

        _currentSessionId = await _sessionOrchestrator!.AllocateSessionWithAffinityAsync(context, affinityOptions, default).ConfigureAwait(false);
        LogSessionAllocated(_logger, _currentSessionId, null);

        RotatingProxySession? httpSession = await _sessionOrchestrator.GetHttpSessionAsync(_currentSessionId, default).ConfigureAwait(false);
        if (httpSession is null)
        {
            LogSessionGetFailed(_logger, _currentSessionId, null);
        }

        return httpSession;
    }

    private SessionAllocationContext CreateSessionAllocationContext(string query, string location)
    {
        return new SessionAllocationContext(
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
    }

    private async IAsyncEnumerable<JsonElement> ExecuteSearchWithPaginationAsync(
        RotatingProxySession httpSession,
        string query,
        string location,
        string? cursor,
        int remaining)
    {
        int itemsRemaining = remaining;
        string? currentCursor = cursor;

        do
        {
            SearchPageResult result = await ExecuteSearchPageAsync(httpSession, query, location, currentCursor, itemsRemaining).ConfigureAwait(false);

            if (!result.Success || result.Document == null)
            {
                await CheckAndRecycleSessionAsync().ConfigureAwait(false);
                yield break;
            }

            yield return result.Document.RootElement.Clone();

            currentCursor = ExtractNextCursor(result.Document);
            if (string.IsNullOrEmpty(currentCursor))
            {
                yield break;
            }

            itemsRemaining -= 25;
        }
        while (itemsRemaining > 0);
    }

    private static string? ExtractNextCursor(JsonDocument document)
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

    private async Task<SearchPageResult> ExecuteSearchPageAsync(
        RotatingProxySession httpSession,
        string query,
        string location,
        string? cursor,
        int remaining)
    {
        await ApplyRateLimitAsync(default).ConfigureAwait(false);

        string formattedQuery = BuildSearchQuery(query, location, remaining);
        var payload = new { query = formattedQuery };

        LogSearchRequest(payload);

        HttpResponseMessage? response = null;
        string content = string.Empty;
        bool success = false;
        Stopwatch stopwatch;
        StartRequestMetrics(out stopwatch);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            (response, content, success) = await TryExecuteSearchRequestAsync(httpSession, payload, attempt).ConfigureAwait(false);

            if (success)
            {
                break;
            }
        }

        EndRequestMetrics(success && response != null && response.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content), stopwatch);

        if (!success || response == null || !response.IsSuccessStatusCode || IsBlockedOrConsentRequired(content))
        {
            return new SearchPageResult { Success = false };
        }

        JsonDocument? document = JsonDocument.Parse(content);
        return new SearchPageResult { Success = true, Document = document };
    }

    private string BuildSearchQuery(string query, string location, int remaining)
    {
        return string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, remaining));
    }

    private void LogSearchRequest(object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        LogRequestPayload(_logger, json, null);
        LogSendingRequest(_logger, _apiEndpoint, null);

        foreach (KeyValuePair<string, IEnumerable<string>> header in _httpClient.DefaultRequestHeaders)
        {
            LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
        }

        try
        {
            LogUsingCountryForRequest(_logger, _country, null);
        }
        catch (Exception)
        {
            // Logging failure ignored intentionally
        }
    }

    private async Task<(HttpResponseMessage? Response, string Content, bool Success)> TryExecuteSearchRequestAsync(
        RotatingProxySession httpSession,
        object payload,
        int attempt)
    {
        HttpResponseMessage? response = null;
        string content = string.Empty;

        try
        {
            using HttpRequestMessage request = CreateRequest(payload);
            LogRequestHeaders(request);

            response = await httpSession.ExecuteAsync(() => request, default).ConfigureAwait(false);

            if ((int)response.StatusCode == 429)
            {
                await DelayForRetryAsync(attempt, 1000).ConfigureAwait(false);
                return (null, string.Empty, false);
            }

            LogResponseStatus(_logger, response.StatusCode.ToString(), null);

            content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            LogResponseContent(_logger, content, null);

            TryWriteDebugLog(content, attempt);

            response.EnsureSuccessStatusCode();

            if (IsBlockedOrConsentRequired(content))
            {
                if (attempt < 2)
                {
                    await DelayForRetryAsync(attempt, 2000).ConfigureAwait(false);
                    return (null, string.Empty, false);
                }
                return (response, content, false);
            }

            return (response, content, true);
        }
        catch (HttpRequestException) when (attempt < 2)
        {
            await DelayForRetryAsync(attempt, 1000).ConfigureAwait(false);
            return (null, string.Empty, false);
        }
    }

    private void LogRequestHeaders(HttpRequestMessage request)
    {
        foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
        {
            LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
        }

        if (request.Content?.Headers != null)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Content.Headers)
            {
                LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
            }
        }
    }

    private static void TryWriteDebugLog(string content, int attempt)
    {
        try
        {
            System.IO.File.WriteAllText($"logs/indeed_jobs_search_{attempt}.json", content);
        }
        catch (Exception ex)
        {
            // Intentionally swallow debug logging errors - directory may not exist or permissions may be insufficient
            Console.Error.WriteLine($"[DEBUG] Failed to write debug log file for attempt {attempt}: {ex.Message}");
        }
    }

    private static async Task DelayForRetryAsync(int attempt, int baseMilliseconds)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * baseMilliseconds)).ConfigureAwait(false);
    }

    private async Task CloseCurrentSessionAsync()
    {
        if (_currentSessionId == null)
        {
            return;
        }

        await _sessionOrchestrator!.CloseSessionAsync(_currentSessionId, default).ConfigureAwait(false);
        _currentSessionId = null;
    }

    private sealed class SearchPageResult
    {
        public bool Success { get; init; }
        public JsonDocument? Document { get; init; }
    }

    private async IAsyncEnumerable<JsonElement> SearchLegacyAsync(string query, string location, int limit)
    {
        string? cursor = null;
        int remaining = limit;

        LogRequestStart(_logger, query, location, null);

        do
        {
            LegacyPageResult result = await ExecuteLegacySearchPageAsync(query, location, cursor, remaining).ConfigureAwait(false);

            if (!result.Success || result.Document == null)
            {
                yield break;
            }

            yield return result.Document.RootElement.Clone();

            cursor = ExtractNextCursor(result.Document);
            if (string.IsNullOrEmpty(cursor))
            {
                yield break;
            }

            remaining -= 25;
        }
        while (remaining > 0);
    }

    private async Task<LegacyPageResult> ExecuteLegacySearchPageAsync(
        string query,
        string location,
        string? cursor,
        int remaining)
    {
        await ApplyRateLimitAsync(default).ConfigureAwait(false);

        string formattedQuery = FormatSearchQueryWithCursor(query, location, cursor, remaining);
        var payload = new { query = formattedQuery };

        LogLegacySearchRequest(payload);

        HttpResponseMessage? response = null;
        string content = string.Empty;
        Stopwatch stopwatch;
        StartRequestMetrics(out stopwatch);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            (response, content) = await TryExecuteLegacyRequestAsync(payload, attempt).ConfigureAwait(false);

            if (response != null && response.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content))
            {
                break;
            }
        }

        EndRequestMetrics(response != null && response.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content), stopwatch);

        if (response == null || !response.IsSuccessStatusCode || IsBlockedOrConsentRequired(content))
        {
            return new LegacyPageResult { Success = false };
        }

        JsonDocument? document = JsonDocument.Parse(content);
        return new LegacyPageResult { Success = true, Document = document };
    }

    private string FormatSearchQueryWithCursor(string query, string location, string? cursor, int remaining)
    {
        string formattedQuery = string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, remaining));

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            formattedQuery += $" after: \"{cursor}\"";
        }

        return formattedQuery;
    }

    private void LogLegacySearchRequest(object payload)
    {
        string json = JsonSerializer.Serialize(payload);
        LogRequestPayload(_logger, json, null);
        LogSendingRequest(_logger, _apiEndpoint, null);

        foreach (KeyValuePair<string, IEnumerable<string>> header in _httpClient.DefaultRequestHeaders)
        {
            LogRequestHeader(_logger, header.Key, string.Join(",", header.Value), null);
        }

        try
        {
            LogUsingCountryForRequest(_logger, _country, null);
        }
        catch (Exception)
        {
            // Logging failure ignored intentionally
        }
    }

    private async Task<(HttpResponseMessage? Response, string Content)> TryExecuteLegacyRequestAsync(
        object payload,
        int attempt)
    {
        HttpResponseMessage? response = null;
        string content = string.Empty;

        try
        {
            using HttpRequestMessage request = CreateRequest(payload);
            LogRequestHeaders(request);

            response = await _httpClient.SendAsync(request).ConfigureAwait(false);

            if ((int)response.StatusCode == 429)
            {
                await DelayForRetryAsync(attempt, 1000).ConfigureAwait(false);
                return (null, string.Empty);
            }

            LogResponseStatus(_logger, response.StatusCode.ToString(), null);

            content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            LogResponseContent(_logger, content, null);

            TryWriteDebugLog(content, attempt);

            response.EnsureSuccessStatusCode();

            if (IsBlockedOrConsentRequired(content))
            {
                if (attempt < 2)
                {
                    await DelayForRetryAsync(attempt, 2000).ConfigureAwait(false);
                    return (null, string.Empty);
                }
                return (response, content);
            }

            return (response, content);
        }
        catch (HttpRequestException) when (attempt < 2)
        {
            await DelayForRetryAsync(attempt, 1000).ConfigureAwait(false);
            return (null, string.Empty);
        }
    }

    private sealed class LegacyPageResult
    {
        public bool Success { get; init; }
        public JsonDocument? Document { get; init; }
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
                LogSessionRecycled(_logger, _currentSessionId, null);
                await _sessionOrchestrator.RecycleSessionAsync(_currentSessionId, default).ConfigureAwait(false);
                _currentSessionId = null;
            }
        }
        catch (Exception ex)
        {
            LogSessionHealthCheckFailed(_logger, _currentSessionId ?? "unknown", ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        _rateLimitSemaphore?.Dispose();

        try
        {
            _httpClient?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        try
        {
            _handler?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        if (_currentSessionId != null && _sessionOrchestrator != null)
        {
            try
            {
                await _sessionOrchestrator.CloseSessionAsync(_currentSessionId, default).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                LogSessionCloseFailed(_logger, _currentSessionId, ex);
            }
        }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public void Dispose()
    {
        if (_disposed) return;

        _rateLimitSemaphore?.Dispose();

        try
        {
            _httpClient?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        try
        {
            _handler?.Dispose();
        }
        catch
        {
            // Ignore disposal errors
        }

        // Note: We cannot call CloseSessionAsync synchronously here.
        // Callers should use DisposeAsync for proper cleanup.
        // If synchronous disposal is required, the session will be cleaned up by the orchestrator.

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static bool IsBlockedOrConsentRequired(string responseContent)
    {
        if (string.IsNullOrEmpty(responseContent))
            return true;

        // Only check for explicit error indicators at the start of the response
        // Valid job responses will contain {"data":{"jobSearch"...}}
        string trimmed = responseContent.TrimStart();

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

        RotatingProxySession? httpSession = await _sessionOrchestrator!.GetHttpSessionAsync(_currentSessionId!, ct).ConfigureAwait(false);
        if (httpSession is null)
        {
            LogSessionGetFailed(_logger, _currentSessionId ?? "unknown", null);
            return default;
        }

        (string content, bool success) = await ExecutePageRequestWithRetryAsync(httpSession, query, location, limit, cursor, ct).ConfigureAwait(false);

        if (!success || IsBlockedOrConsentRequired(content))
        {
            await CheckAndRecycleSessionAsync().ConfigureAwait(false);
            return default;
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    private async Task<(string Content, bool Success)> ExecutePageRequestWithRetryAsync(
        RotatingProxySession httpSession,
        string query,
        string location,
        int limit,
        string? cursor,
        CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct).ConfigureAwait(false);

        string formattedQuery = BuildFormattedQuery(query, location, limit, cursor);
        var payload = new { query = formattedQuery };

        string content = string.Empty;
        bool success = false;
        HttpResponseMessage? response = null;
        Stopwatch stopwatch;
        StartRequestMetrics(out stopwatch);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            (response, content, success) = await TryExecutePageRequestAsync(httpSession, payload, attempt, ct).ConfigureAwait(false);

            if (success)
            {
                break;
            }
        }

        EndRequestMetrics(success && response != null && response.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content), stopwatch);

        return (content, success && response != null && response.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content));
    }

    private static string BuildFormattedQuery(string query, string location, int limit, string? cursor)
    {
        string formattedQuery = string.Format(System.Globalization.CultureInfo.InvariantCulture, JobSearchQueryFormat, query, location, Math.Min(25, limit));

        if (!string.IsNullOrWhiteSpace(cursor))
        {
            formattedQuery += $" after: \"{cursor}\"";
        }

        return formattedQuery;
    }

    private async Task<(HttpResponseMessage? Response, string Content, bool Success)> TryExecutePageRequestAsync(
        RotatingProxySession httpSession,
        object payload,
        int attempt,
        CancellationToken ct)
    {
        try
        {
            using HttpRequestMessage request = CreateRequest(payload);
            HttpResponseMessage response = await httpSession.ExecuteAsync(() => request, ct).ConfigureAwait(false);

            if ((int)response.StatusCode == 429)
            {
                await DelayForRetryAsync(attempt, 1000, ct).ConfigureAwait(false);
                return (null, string.Empty, false);
            }

            string content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (IsBlockedOrConsentRequired(content))
            {
                if (attempt < 2)
                {
                    await DelayForRetryAsync(attempt, 2000, ct).ConfigureAwait(false);
                    return (null, string.Empty, false);
                }
                return (response, content, false);
            }

            return (response, content, true);
        }
        catch (HttpRequestException) when (attempt < 2)
        {
            await DelayForRetryAsync(attempt, 1000, ct).ConfigureAwait(false);
            return (null, string.Empty, false);
        }
    }

    private static async Task DelayForRetryAsync(int attempt, int baseMilliseconds, CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * baseMilliseconds), ct).ConfigureAwait(false);
    }

    private async Task<JsonElement> SearchPageLegacyAsync(string query, string location, int limit, string? cursor, CancellationToken ct)
    {
        await ApplyRateLimitAsync(ct).ConfigureAwait(false);

        string formattedQuery = BuildFormattedQuery(query, location, limit, cursor);
        var payload = new { query = formattedQuery };

        (string content, HttpResponseMessage? response) = await ExecuteLegacyPageRequestWithRetryAsync(payload, ct).ConfigureAwait(false);

        EndRequestMetrics(response != null && response.IsSuccessStatusCode && !IsBlockedOrConsentRequired(content), null!);

        if (response == null || !response.IsSuccessStatusCode || IsBlockedOrConsentRequired(content))
        {
            return default;
        }

        using var doc = JsonDocument.Parse(content);
        return doc.RootElement.Clone();
    }

    private async Task<(string Content, HttpResponseMessage? Response)> ExecuteLegacyPageRequestWithRetryAsync(
        object payload,
        CancellationToken ct)
    {
        string content = string.Empty;
        HttpResponseMessage? response = null;

        for (int attempt = 0; attempt < 3; attempt++)
        {
            (response, content, bool shouldContinue) = await TryExecuteLegacyPageRequestAsync(payload, attempt, ct).ConfigureAwait(false);

            if (!shouldContinue)
            {
                break;
            }
        }

        return (content, response);
    }

    private async Task<(HttpResponseMessage? Response, string Content, bool ShouldContinue)> TryExecuteLegacyPageRequestAsync(
        object payload,
        int attempt,
        CancellationToken ct)
    {
        try
        {
            using HttpRequestMessage request = CreateRequest(payload);
            HttpResponseMessage response = await _httpClient.SendAsync(request, ct).ConfigureAwait(false);

            if ((int)response.StatusCode == 429)
            {
                await DelayForRetryAsync(attempt, 1000, ct).ConfigureAwait(false);
                return (null, string.Empty, true);
            }

            string content = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (IsBlockedOrConsentRequired(content))
            {
                if (attempt < 2)
                {
                    await DelayForRetryAsync(attempt, 2000, ct).ConfigureAwait(false);
                    return (null, string.Empty, true);
                }
                return (response, content, false);
            }

            return (response, content, false);
        }
        catch (HttpRequestException) when (attempt < 2)
        {
            await DelayForRetryAsync(attempt, 1000, ct).ConfigureAwait(false);
            return (null, string.Empty, true);
        }
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

        string affinityKey = $"indeed_{query}_{location}_{Guid.NewGuid():N}";
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
