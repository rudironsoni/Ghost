using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace Ghost.Scraper.DotnetSpider.StructuredLogging;

/// <summary>
/// Provides comprehensive structured logging for the job scraper system.
/// Logs anti-bot events, parsing strategies, requests/responses, session lifecycle,
/// and proxy pool health with correlation IDs for request tracking.
/// </summary>
public sealed class JobScraperLoggingMiddleware
{
    private readonly ILogger<JobScraperLoggingMiddleware> _logger;

    /// <summary>
    /// Initializes a new instance of the JobScraperLoggingMiddleware.
    /// </summary>
    /// <param name="logger">The logger instance for structured logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public JobScraperLoggingMiddleware(ILogger<JobScraperLoggingMiddleware> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Anti-Bot Event Logging

    /// <summary>
    /// Logs when a consent page is encountered during scraping.
    /// </summary>
    public void LogConsentPageDetected(string platform, string correlationId, string? pageTitle = null)
    {
        _logConsentPageDetected(_logger, platform, correlationId, pageTitle, null);
    }

    /// <summary>
    /// Logs when a CAPTCHA challenge is detected.
    /// </summary>
    public void LogCaptchaDetected(string platform, string correlationId, string? captchaType = null)
    {
        _logCaptchaDetected(_logger, platform, correlationId, captchaType, null);
    }

    /// <summary>
    /// Logs when a request is blocked (403/429/etc).
    /// </summary>
    public void LogRequestBlocked(string platform, string correlationId, int statusCode, string? reason = null)
    {
        _logRequestBlocked(_logger, platform, correlationId, statusCode, reason, null);
    }

    /// <summary>
    /// Logs when rate limiting is encountered.
    /// </summary>
    public void LogRateLimitDetected(string platform, string correlationId, int? retryAfterSeconds = null)
    {
        _logRateLimitDetected(_logger, platform, correlationId, retryAfterSeconds ?? 0, null);
    }

    /// <summary>
    /// Logs anti-bot detection events with details.
    /// </summary>
    public void LogAntiBotDetection(string platform, string correlationId, string detectionType, Dictionary<string, string>? details = null)
    {
        var detailsStr = details != null ? string.Join(", ", details) : "none";
        _logAntiBotDetection(_logger, platform, correlationId, detectionType, detailsStr, null);
    }

    #endregion

    #region Parsing Strategy Logging

    /// <summary>
    /// Logs when a parsing strategy is selected.
    /// </summary>
    public void LogParsingStrategySelected(string platform, string strategy, string correlationId)
    {
        _logParsingStrategySelected(_logger, platform, strategy, correlationId, null);
    }

    /// <summary>
    /// Logs the results of parsing with a selected strategy.
    /// </summary>
    public void LogParsingStrategyResult(string platform, string strategy, int itemsFound, bool success, string correlationId, Exception? ex = null)
    {
        _logParsingStrategyResult(_logger, platform, strategy, itemsFound, success, correlationId, ex);
    }

    /// <summary>
    /// Logs when a parsing strategy fails and a fallback is attempted.
    /// </summary>
    public void LogParsingStrategyFallback(string platform, string failedStrategy, string fallbackStrategy, string correlationId)
    {
        _logParsingStrategyFallback(_logger, platform, failedStrategy, fallbackStrategy, correlationId, null);
    }

    #endregion

    #region Request/Response Logging

    /// <summary>
    /// Logs HTTP request details (sanitized - no PII).
    /// </summary>
    public void LogHttpRequest(string platform, string correlationId, string method, string? url = null, Dictionary<string, string>? headers = null)
    {
        var headersStr = headers != null && headers.Count > 0 ? string.Join(", ", headers.Keys) : "none";
        _logHttpRequest(_logger, platform, correlationId, method, url ?? "unknown", headersStr, null);
    }

    /// <summary>
    /// Logs HTTP response details.
    /// </summary>
    public void LogHttpResponse(string platform, string correlationId, int statusCode, int? contentLength = null, long? elapsedMs = null)
    {
        _logHttpResponse(_logger, platform, correlationId, statusCode, contentLength ?? 0, elapsedMs ?? 0, null);
    }

    /// <summary>
    /// Logs HTTP request/response errors.
    /// </summary>
    public void LogHttpError(string platform, string correlationId, string errorType, string? message = null)
    {
        _logHttpError(_logger, platform, correlationId, errorType, message ?? "unknown", null);
    }

    /// <summary>
    /// Logs HTTP request/response errors with exception details.
    /// </summary>
    public void LogHttpErrorWithException(string platform, string correlationId, string errorType, Exception ex, string? message = null)
    {
        _logHttpErrorWithException(_logger, platform, correlationId, errorType, message ?? ex.Message, ex);
    }

    #endregion

    #region Session Lifecycle Logging

    /// <summary>
    /// Logs when a scraping session is created.
    /// </summary>
    public void LogSessionCreated(string sessionId, string correlationId, string platform, string? sessionType = null)
    {
        _logSessionCreated(_logger, sessionId, correlationId, platform, sessionType ?? "unknown", null);
    }

    /// <summary>
    /// Logs when a session is used/accessed.
    /// </summary>
    public void LogSessionUsed(string sessionId, string correlationId, int requestCount = 1)
    {
        _logSessionUsed(_logger, sessionId, correlationId, requestCount, null);
    }

    /// <summary>
    /// Logs when a session is disposed/released.
    /// </summary>
    public void LogSessionDisposed(string sessionId, string correlationId, long? totalDurationMs = null)
    {
        _logSessionDisposed(_logger, sessionId, correlationId, totalDurationMs ?? 0, null);
    }

    /// <summary>
    /// Logs session lifecycle errors.
    /// </summary>
    public void LogSessionError(string sessionId, string correlationId, string errorType)
    {
        _logSessionError(_logger, sessionId, correlationId, errorType, null);
    }

    /// <summary>
    /// Logs session lifecycle errors with exception details.
    /// </summary>
    public void LogSessionErrorWithException(string sessionId, string correlationId, string errorType, Exception ex)
    {
        _logSessionErrorWithException(_logger, sessionId, correlationId, errorType, ex);
    }

    #endregion

    #region Pool Health Logging

    /// <summary>
    /// Logs proxy pool health status.
    /// </summary>
    public void LogProxyPoolHealth(string correlationId, int totalProxies, int healthyProxies, int unhealthyProxies)
    {
        _logProxyPoolHealth(_logger, correlationId, totalProxies, healthyProxies, unhealthyProxies, null);
    }

    /// <summary>
    /// Logs proxy rotation events.
    /// </summary>
    public void LogProxyRotation(string correlationId, string? fromProxy = null, string? toProxy = null, string? reason = null)
    {
        _logProxyRotation(_logger, correlationId, fromProxy ?? "unknown", toProxy ?? "unknown", reason ?? "rotation", null);
    }

    /// <summary>
    /// Logs when a proxy is marked as unhealthy.
    /// </summary>
    public void LogProxyUnhealthy(string correlationId, string proxy, string reason, int failureCount = 1)
    {
        _logProxyUnhealthy(_logger, correlationId, proxy, reason, failureCount, null);
    }

    /// <summary>
    /// Logs proxy pool errors.
    /// </summary>
    public void LogProxyPoolError(string correlationId, string errorType, string? message = null)
    {
        _logProxyPoolError(_logger, correlationId, errorType, message ?? "unknown", null);
    }

    /// <summary>
    /// Logs proxy pool errors with exception details.
    /// </summary>
    public void LogProxyPoolErrorWithException(string correlationId, string errorType, Exception ex, string? message = null)
    {
        _logProxyPoolErrorWithException(_logger, correlationId, errorType, message ?? ex.Message, ex);
    }

    #endregion

    #region General Logging Methods

    /// <summary>
    /// Logs a general scraping operation.
    /// </summary>
    public void LogScrapingOperation(string platform, string correlationId, string operation, Dictionary<string, object>? properties = null)
    {
        var propStr = properties != null && properties.Count > 0 
            ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}"))
            : "none";
        _logScrapingOperation(_logger, platform, correlationId, operation, propStr, null);
    }

    /// <summary>
    /// Logs a general informational message with structured properties.
    /// </summary>
    public void LogInfo(string message, Dictionary<string, object>? properties = null)
    {
        var propStr = properties != null && properties.Count > 0
            ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}"))
            : "none";
        _logInfo(_logger, message, propStr, null);
    }

    /// <summary>
    /// Logs a warning.
    /// </summary>
    public void LogWarning(string message, string? details = null)
    {
        _logWarning(_logger, message, details ?? "none", null);
    }

    /// <summary>
    /// Logs an error.
    /// </summary>
    public void LogError(string message)
    {
        _logError(_logger, message, null);
    }

    /// <summary>
    /// Logs an error with exception details.
    /// </summary>
    public void LogErrorWithException(string message, Exception ex)
    {
        _logErrorWithException(_logger, message, ex);
    }

    #endregion

    #region LoggerMessage.Define Static Actions

    // Anti-Bot Events
    private static readonly Action<ILogger, string, string, string?, Exception?> _logConsentPageDetected =
        LoggerMessage.Define<string, string, string?>(
            LogLevel.Information,
            new EventId(2001, nameof(LogConsentPageDetected)),
            "Consent page detected for platform {Platform} (CorrelationId: {CorrelationId}, PageTitle: {PageTitle})");

    private static readonly Action<ILogger, string, string, string?, Exception?> _logCaptchaDetected =
        LoggerMessage.Define<string, string, string?>(
            LogLevel.Warning,
            new EventId(2002, nameof(LogCaptchaDetected)),
            "CAPTCHA detected for platform {Platform} (CorrelationId: {CorrelationId}, Type: {CaptchaType})");

    private static readonly Action<ILogger, string, string, int, string?, Exception?> _logRequestBlocked =
        LoggerMessage.Define<string, string, int, string?>(
            LogLevel.Warning,
            new EventId(2003, nameof(LogRequestBlocked)),
            "Request blocked for platform {Platform} (CorrelationId: {CorrelationId}, Status: {StatusCode}, Reason: {Reason})");

    private static readonly Action<ILogger, string, string, int, Exception?> _logRateLimitDetected =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Warning,
            new EventId(2004, nameof(LogRateLimitDetected)),
            "Rate limit detected for platform {Platform} (CorrelationId: {CorrelationId}, RetryAfter: {RetryAfterSeconds}s)");

    private static readonly Action<ILogger, string, string, string, string, Exception?> _logAntiBotDetection =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Warning,
            new EventId(2005, nameof(LogAntiBotDetection)),
            "Anti-bot detection for platform {Platform} (CorrelationId: {CorrelationId}, Type: {DetectionType}, Details: {Details})");

    // Parsing Strategy
    private static readonly Action<ILogger, string, string, string, Exception?> _logParsingStrategySelected =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Debug,
            new EventId(2010, nameof(LogParsingStrategySelected)),
            "Parsing strategy selected for platform {Platform}: {Strategy} (CorrelationId: {CorrelationId})");

    private static readonly Action<ILogger, string, string, int, bool, string, Exception?> _logParsingStrategyResult =
        LoggerMessage.Define<string, string, int, bool, string>(
            LogLevel.Information,
            new EventId(2011, nameof(LogParsingStrategyResult)),
            "Parsing strategy result for platform {Platform}, strategy {Strategy}: Found {ItemsFound} items, Success: {Success} (CorrelationId: {CorrelationId})");

    private static readonly Action<ILogger, string, string, string, string, Exception?> _logParsingStrategyFallback =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information,
            new EventId(2012, nameof(LogParsingStrategyFallback)),
            "Parsing fallback for platform {Platform}: {FailedStrategy} -> {FallbackStrategy} (CorrelationId: {CorrelationId})");

    // Request/Response
    private static readonly Action<ILogger, string, string, string, string, string, Exception?> _logHttpRequest =
        LoggerMessage.Define<string, string, string, string, string>(
            LogLevel.Debug,
            new EventId(2020, nameof(LogHttpRequest)),
            "HTTP request for platform {Platform} (CorrelationId: {CorrelationId}): {Method} {Url}, Headers: {Headers}");

    private static readonly Action<ILogger, string, string, int, int, long, Exception?> _logHttpResponse =
        LoggerMessage.Define<string, string, int, int, long>(
            LogLevel.Debug,
            new EventId(2021, nameof(LogHttpResponse)),
            "HTTP response for platform {Platform} (CorrelationId: {CorrelationId}): Status {StatusCode}, ContentLength: {ContentLength}, Elapsed: {ElapsedMs}ms");

    private static readonly Action<ILogger, string, string, string, string, Exception?> _logHttpError =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Error,
            new EventId(2022, nameof(LogHttpError)),
            "HTTP error for platform {Platform} (CorrelationId: {CorrelationId}): {ErrorType} - {Message}");

    private static readonly Action<ILogger, string, string, string, string, Exception> _logHttpErrorWithException =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Error,
            new EventId(2022, nameof(LogHttpErrorWithException)),
            "HTTP error for platform {Platform} (CorrelationId: {CorrelationId}): {ErrorType} - {Message}");

    // Session Lifecycle
    private static readonly Action<ILogger, string, string, string, string, Exception?> _logSessionCreated =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information,
            new EventId(2030, nameof(LogSessionCreated)),
            "Session created (SessionId: {SessionId}, CorrelationId: {CorrelationId}, Platform: {Platform}, Type: {SessionType})");

    private static readonly Action<ILogger, string, string, int, Exception?> _logSessionUsed =
        LoggerMessage.Define<string, string, int>(
            LogLevel.Debug,
            new EventId(2031, nameof(LogSessionUsed)),
            "Session used (SessionId: {SessionId}, CorrelationId: {CorrelationId}, RequestCount: {RequestCount})");

    private static readonly Action<ILogger, string, string, long, Exception?> _logSessionDisposed =
        LoggerMessage.Define<string, string, long>(
            LogLevel.Information,
            new EventId(2032, nameof(LogSessionDisposed)),
            "Session disposed (SessionId: {SessionId}, CorrelationId: {CorrelationId}, TotalDurationMs: {TotalDurationMs})");

    private static readonly Action<ILogger, string, string, string, Exception?> _logSessionError =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(2033, nameof(LogSessionError)),
            "Session error (SessionId: {SessionId}, CorrelationId: {CorrelationId}, ErrorType: {ErrorType})");

    private static readonly Action<ILogger, string, string, string, Exception> _logSessionErrorWithException =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(2033, nameof(LogSessionErrorWithException)),
            "Session error (SessionId: {SessionId}, CorrelationId: {CorrelationId}, ErrorType: {ErrorType})");

    // Proxy Pool Health
    private static readonly Action<ILogger, string, int, int, int, Exception?> _logProxyPoolHealth =
        LoggerMessage.Define<string, int, int, int>(
            LogLevel.Information,
            new EventId(2040, nameof(LogProxyPoolHealth)),
            "Proxy pool health (CorrelationId: {CorrelationId}): Total={TotalProxies}, Healthy={HealthyProxies}, Unhealthy={UnhealthyProxies}");

    private static readonly Action<ILogger, string, string, string, string, Exception?> _logProxyRotation =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Debug,
            new EventId(2041, nameof(LogProxyRotation)),
            "Proxy rotation (CorrelationId: {CorrelationId}): {FromProxy} -> {ToProxy}, Reason: {Reason}");

    private static readonly Action<ILogger, string, string, string, int, Exception?> _logProxyUnhealthy =
        LoggerMessage.Define<string, string, string, int>(
            LogLevel.Warning,
            new EventId(2042, nameof(LogProxyUnhealthy)),
            "Proxy marked unhealthy (CorrelationId: {CorrelationId}, Proxy: {Proxy}, Reason: {Reason}, FailureCount: {FailureCount})");

    private static readonly Action<ILogger, string, string, string, Exception?> _logProxyPoolError =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(2043, nameof(LogProxyPoolError)),
            "Proxy pool error (CorrelationId: {CorrelationId}, ErrorType: {ErrorType}, Message: {Message})");

    private static readonly Action<ILogger, string, string, string, Exception> _logProxyPoolErrorWithException =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Error,
            new EventId(2043, nameof(LogProxyPoolErrorWithException)),
            "Proxy pool error (CorrelationId: {CorrelationId}, ErrorType: {ErrorType}, Message: {Message})");

    // General
    private static readonly Action<ILogger, string, string, string, string, Exception?> _logScrapingOperation =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Information,
            new EventId(2050, nameof(LogScrapingOperation)),
            "Scraping operation for platform {Platform} (CorrelationId: {CorrelationId}): {Operation}, Properties: {Properties}");

    private static readonly Action<ILogger, string, string, Exception?> _logInfo =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(2051, nameof(LogInfo)),
            "{Message} (Properties: {Properties})");

    private static readonly Action<ILogger, string, string, Exception?> _logWarning =
        LoggerMessage.Define<string, string>(
            LogLevel.Warning,
            new EventId(2052, nameof(LogWarning)),
            "{Message} (Details: {Details})");

    private static readonly Action<ILogger, string, Exception?> _logError =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2053, nameof(LogError)),
            "{Message}");

    private static readonly Action<ILogger, string, Exception> _logErrorWithException =
        LoggerMessage.Define<string>(
            LogLevel.Error,
            new EventId(2053, nameof(LogErrorWithException)),
            "{Message}");

    #endregion
}
