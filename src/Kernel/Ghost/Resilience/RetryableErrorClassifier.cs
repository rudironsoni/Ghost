using System;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Ghost.Resilience;

/// <summary>
/// Classifies exceptions and HTTP status codes for retry eligibility.
/// </summary>
public static class RetryableErrorClassifier
{
    /// <summary>
    /// Determines whether an HTTP status code represents a retryable response.
    /// </summary>
    /// <param name="code">The HTTP status code to evaluate.</param>
    /// <returns>True if the status code is retryable; otherwise false.</returns>
    public static bool IsRetryable(HttpStatusCode code)
    {
        return code == HttpStatusCode.TooManyRequests ||
               code == HttpStatusCode.ServiceUnavailable ||
               code == HttpStatusCode.GatewayTimeout;
    }

    /// <summary>
    /// Determines whether an exception represents a retryable failure.
    /// </summary>
    /// <param name="ex">The exception to evaluate.</param>
    /// <returns>True if the exception is retryable; otherwise false.</returns>
    public static bool IsRetryable(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        if (ex is TaskCanceledException)
        {
            return true;
        }

        if (ex is HttpRequestException)
        {
            return true;
        }

        if (ex is ValidationException)
        {
            return false;
        }

        if (ex is JsonException)
        {
            return false;
        }

        if (ex is InvalidOperationException)
        {
            return false;
        }

        return false;
    }
}
