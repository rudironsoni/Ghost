using System;
using System.Net;
using System.Net.Http;
using Ghost.Contracts.Jobs;

namespace Ghost.Kernel.Services;

/// <summary>
/// Service for categorizing and providing suggestions for job search errors.
/// </summary>
public static class ErrorCategorizationService
{
    /// <summary>
    /// Categorizes an exception and provides structured error information.
    /// </summary>
    /// <param name="exception">The exception to categorize</param>
    /// <param name="platformName">Name of the platform where the error occurred</param>
    /// <returns>Structured error information</returns>
    public static PlatformError CategorizeError(Exception exception, string platformName)
    {
        string errorCategory = DetermineErrorCategory(exception);
        string message = GetErrorMessage(exception);
        string suggestion = GetErrorSuggestion(errorCategory, exception);
        bool retryable = IsRetryable(errorCategory, exception);

        return new PlatformError
        {
            Platform = platformName,
            ErrorCategory = errorCategory,
            Message = message,
            TechnicalDetails = exception.ToString(),
            Suggestion = suggestion,
            Retryable = retryable,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Categorizes an HTTP response error and provides structured error information.
    /// </summary>
    /// <param name="response">The HTTP response that failed</param>
    /// <param name="platformName">Name of the platform where the error occurred</param>
    /// <returns>Structured error information</returns>
    public static PlatformError CategorizeHttpError(HttpResponseMessage response, string platformName)
    {
        int statusCode = (int)response.StatusCode;
        string errorCategory = DetermineHttpErrorCategory(statusCode);
        string message = GetHttpErrorMessage(response.StatusCode);
        string suggestion = GetErrorSuggestion(errorCategory, null);
        bool retryable = IsRetryable(errorCategory, null);

        return new PlatformError
        {
            Platform = platformName,
            ErrorCategory = errorCategory,
            Message = message,
            TechnicalDetails = $"HTTP {statusCode}: {response.ReasonPhrase}",
            Suggestion = suggestion,
            Retryable = retryable,
            Timestamp = DateTime.UtcNow
        };
    }

    private static string DetermineErrorCategory(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => "Network",
            TaskCanceledException => "Timeout",
            OperationCanceledException => "Cancelled",
            UnauthorizedAccessException => "Auth",
            ArgumentException or ArgumentNullException => "Configuration",
            InvalidOperationException => "Parse",
            _ => "Unknown"
        };
    }

    private static string DetermineHttpErrorCategory(int statusCode)
    {
        return statusCode switch
        {
            401 or 403 => "Auth",
            404 => "NotFound",
            429 => "RateLimit",
            >= 500 and < 600 => "Server",
            >= 400 and < 500 => "Client",
            _ => "Unknown"
        };
    }

    private static string GetErrorMessage(Exception exception)
    {
        return exception switch
        {
            HttpRequestException httpEx => $"Network error: {httpEx.Message}",
            TaskCanceledException => "Request timed out",
            OperationCanceledException => "Request was cancelled",
            UnauthorizedAccessException => "Authentication failed",
            ArgumentException argEx => $"Invalid argument: {argEx.Message}",
            InvalidOperationException invEx => $"Parse error: {invEx.Message}",
            _ => exception.Message
        };
    }

    private static string GetHttpErrorMessage(HttpStatusCode statusCode)
    {
        return statusCode switch
        {
            HttpStatusCode.Unauthorized => "Authentication required",
            HttpStatusCode.Forbidden => "Access forbidden",
            HttpStatusCode.NotFound => "Resource not found",
            HttpStatusCode.TooManyRequests => "Rate limit exceeded",
            HttpStatusCode.InternalServerError => "Internal server error",
            HttpStatusCode.BadGateway => "Bad gateway",
            HttpStatusCode.ServiceUnavailable => "Service unavailable",
            HttpStatusCode.GatewayTimeout => "Gateway timeout",
            _ => $"HTTP error: {statusCode}"
        };
    }

    private static string GetErrorSuggestion(string errorCategory, Exception? exception)
    {
        return errorCategory switch
        {
            "Auth" => "Check API credentials and authentication tokens",
            "Network" => "Verify internet connection and try again",
            "RateLimit" => "Wait before retrying or reduce request frequency",
            "Server" => "The service is temporarily unavailable, try again later",
            "Timeout" => "Increase timeout settings or try with browser fallback",
            "Parse" => "The website structure may have changed, parser needs updating",
            "Configuration" => "Check configuration settings and parameters",
            "Cancelled" => "Request was cancelled by user or timeout",
            _ => "Check logs for more details and try again"
        };
    }

    private static bool IsRetryable(string errorCategory, Exception? exception)
    {
        return errorCategory switch
        {
            "Network" => true,
            "Server" => true,
            "RateLimit" => true,
            "Timeout" => true,
            "Auth" => false,
            "Parse" => false,
            "Configuration" => false,
            "Cancelled" => false,
            _ => false
        };
    }
}
