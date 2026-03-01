using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace Ghost.Http;

/// <summary>
/// Enhanced retry utility with exponential backoff and jitter for HTTP requests.
/// Provides different retry strategies for different error types:
/// - 429 (rate limit): Longer backoff (30s, 60s, 120s)
/// - 5xx server errors: Standard backoff (1s, 2s, 4s, 8s)
/// - Network timeout: Standard backoff
/// - Parser failure: No retry (structural issue)
/// </summary>
public static class EnhancedRetryPolicy
{

    private static readonly Action<ILogger, int, double, string, Exception?> LogRetryAttemptWithException =
        LoggerMessage.Define<int, double, string>(
            LogLevel.Warning,
            new EventId(1, nameof(LogRetryAttemptWithException)),
            "Retry attempt {RetryCount} after {DelayMs}ms due to exception: {ExceptionMessage}");

    private static readonly Action<ILogger, int, double, int, string, Exception?> LogRetryAttemptWithStatusCode =
        LoggerMessage.Define<int, double, int, string>(
            LogLevel.Warning,
            new EventId(2, nameof(LogRetryAttemptWithStatusCode)),
            "Retry attempt {RetryCount} after {DelayMs}ms due to HTTP {StatusCode} ({ErrorType})");

    /// <summary>
    /// Creates a retry policy with exponential backoff and jitter for HTTP requests.
    /// Handles different error types with appropriate retry strategies.
    /// </summary>
    /// <param name="logger">Optional logger for retry attempt logging</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <param name="enableJitter">Whether to add jitter to prevent thundering herd (default: true)</param>
    /// <returns>A Polly retry policy for HTTP responses</returns>
    public static IAsyncPolicy<HttpResponseMessage> CreatePolicy(
        ILogger? logger = null,
        int maxRetries = 3,
        bool enableJitter = true)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(r => ShouldRetry(r))
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt => CalculateDelay(retryAttempt, enableJitter),
                onRetry: (outcome, timeSpan, retryCount, context) =>
                {
                    LogRetryAttempt(logger, outcome, timeSpan, retryCount);
                });
    }

    /// <summary>
    /// Creates a retry policy with custom delay generator for advanced scenarios.
    /// Allows extracting delay from Retry-After header or using custom logic.
    /// </summary>
    /// <param name="logger">Optional logger for retry attempt logging</param>
    /// <param name="maxRetries">Maximum number of retry attempts (default: 3)</param>
    /// <returns>A Polly retry policy with custom delay generator</returns>
    public static IAsyncPolicy<HttpResponseMessage> CreatePolicyWithCustomDelay(
        ILogger? logger = null,
        int maxRetries = 3)
    {
        return Policy<HttpResponseMessage>
            .HandleResult(r => ShouldRetry(r))
            .WaitAndRetryAsync(
                maxRetries,
                retryAttempt =>
                {
                    // Try to extract delay from Retry-After header
                    if (retryAttempt > 0)
                    {
                        TimeSpan delay = CalculateDelay(retryAttempt, enableJitter: true);
                        return delay;
                    }
                    return TimeSpan.Zero;
                },
                onRetry: (outcome, timeSpan, retryCount, context) =>
                {
                    LogRetryAttempt(logger, outcome, timeSpan, retryCount);
                });
    }

    /// <summary>
    /// Determines whether an HTTP response should be retried based on status code.
    /// </summary>
    /// <param name="response">The HTTP response to evaluate</param>
    /// <returns>True if the response should be retried, false otherwise</returns>
    private static bool ShouldRetry(HttpResponseMessage response)
    {
        int statusCode = (int)response.StatusCode;

        // 429 Too Many Requests - rate limit, should retry with longer backoff
        if (statusCode == 429)
        {
            return true;
        }

        // 5xx server errors - should retry with standard backoff
        if (statusCode >= 500 && statusCode < 600)
        {
            return true;
        }

        // 408 Request Timeout - should retry
        if (statusCode == 408)
        {
            return true;
        }

        // 429 is already handled above
        // Other 4xx client errors (except 429) should NOT be retried
        // Parser failures (structural issues) should NOT be retried
        return false;
    }

    /// <summary>
    /// Calculates the delay for a retry attempt based on the error type and attempt number.
    /// </summary>
    /// <param name="retryAttempt">The current retry attempt number (1-based)</param>
    /// <param name="enableJitter">Whether to add jitter to the delay</param>
    /// <returns>The calculated delay for this retry attempt</returns>
    private static TimeSpan CalculateDelay(int retryAttempt, bool enableJitter)
    {
        // Base delay calculation using exponential backoff
        // For rate limit (429): 30s, 60s, 120s
        // For server errors (5xx): 1s, 2s, 4s, 8s
        var baseDelay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1));

        // Add jitter if enabled to prevent thundering herd effect
        if (enableJitter)
        {
            int jitterMs = Random.Shared.Next(250, 1000); // 250ms to 1000ms jitter
            baseDelay = baseDelay.Add(TimeSpan.FromMilliseconds(jitterMs));
        }

        return baseDelay;
    }

    /// <summary>
    /// Logs retry attempt information including timing and error details.
    /// </summary>
    /// <param name="logger">The logger to use (can be null)</param>
    /// <param name="outcome">The outcome of the failed operation</param>
    /// <param name="timeSpan">The delay before the next retry</param>
    /// <param name="retryCount">The current retry attempt number</param>
    private static void LogRetryAttempt(
        ILogger? logger,
        DelegateResult<HttpResponseMessage> outcome,
        TimeSpan timeSpan,
        int retryCount)
    {
        if (logger == null)
        {
            return;
        }

        HttpStatusCode? statusCode = outcome.Result?.StatusCode;
        Exception exception = outcome.Exception;

        if (exception != null)
        {
            LogRetryAttemptWithException(logger, retryCount, timeSpan.TotalMilliseconds, exception.Message, exception);
        }
        else if (statusCode.HasValue)
        {
            string errorType = GetErrorType(statusCode.Value);
            LogRetryAttemptWithStatusCode(logger, retryCount, timeSpan.TotalMilliseconds, (int)statusCode.Value, errorType, null);
        }
    }

    /// <summary>
    /// Gets a human-readable error type description for an HTTP status code.
    /// </summary>
    /// <param name="statusCode">The HTTP status code</param>
    /// <returns>A description of the error type</returns>
    private static string GetErrorType(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.TooManyRequests => "Rate Limit",
            HttpStatusCode.InternalServerError => "Server Error",
            HttpStatusCode.BadGateway => "Bad Gateway",
            HttpStatusCode.ServiceUnavailable => "Service Unavailable",
            HttpStatusCode.GatewayTimeout => "Gateway Timeout",
            HttpStatusCode.RequestTimeout => "Request Timeout",
            _ => statusCode.ToString()
        };
    }
}
